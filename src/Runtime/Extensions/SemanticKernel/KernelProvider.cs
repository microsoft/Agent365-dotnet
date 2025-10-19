// ------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------------------------

using System.Collections.Concurrent;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.A365.Runtime.Extensions.SemanticKernel
{
    /// <summary>
    /// Provides caching and management of <see cref="Kernel"/> instances per tenant and worker,
    /// including support for plugin/function copying, governance, and cache eviction.
    /// </summary>
    public class KernelProvider : IKernelProvider, IDisposable
    {
        private readonly ConcurrentDictionary<(string, string), (Kernel kernel, DateTime lastUsed)> _kernelCache = new();
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(1); // Start with 1 hour expiry
        private readonly Timer _evictionTimer;
        private bool _disposed;
        private Action<IKernelBuilder> _configureKernelBuilder;
        private Kernel? _templateKernel;
        private readonly IServiceProvider? _serviceProvider;
        private readonly IGovernanceDelegateFactory? _governanceDelegateFactory;
        private readonly ILogger? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="KernelProvider"/> class.
        /// </summary>
        /// <param name="configureKernelBuilder">An action to configure the <see cref="IKernelBuilder"/> for kernel creation.</param>
        /// <param name="serviceProvider">Optional service provider for dependency injection.</param>
        public KernelProvider(Action<IKernelBuilder> configureKernelBuilder, IServiceProvider? serviceProvider = null)
        {
            _configureKernelBuilder = configureKernelBuilder ?? (_ => { });
            _serviceProvider = serviceProvider;
            _governanceDelegateFactory = serviceProvider?.GetService<IGovernanceDelegateFactory>();
            _logger = serviceProvider?.GetService<ILogger<KernelProvider>>();
            _evictionTimer = new Timer(EvictExpiredKernels, null, _cacheExpiry, _cacheExpiry);
        }


        /// <summary>
        /// Sets the action used to configure the <see cref="IKernelBuilder"/> for kernel creation.
        /// </summary>
        /// <param name="configure">An action to configure the <see cref="IKernelBuilder"/>.</param>
        public void SetKernelBuilder(Action<IKernelBuilder> configure)
        {
            _configureKernelBuilder = configure ?? (_ => { });
        }

        /// <inheritdoc/>
        public Kernel GetKernel(string tenantId, string workerId)
        {
            // Create governance delegate automatically if factory is available
            var governanceDelegate = _governanceDelegateFactory?.CreateGovernanceDelegate(_logger);

            _logger?.LogTrace("Resolving Kernel for Tenant='{tenant}', Worker='{worker}'", tenantId, workerId);

            var kernel = GetKernel(tenantId, workerId, governanceDelegate);

            return kernel;
        }

        /// <inheritdoc/>
        public Kernel GetKernel(string tenantId, string workerId, Func<Kernel, Task>? onCacheMiss)
        {
            ArgumentNullException.ThrowIfNull(tenantId);
            ArgumentNullException.ThrowIfNull(workerId);

            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID cannot be empty or whitespace.", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(workerId))
                throw new ArgumentException("Worker ID cannot be empty or whitespace.", nameof(workerId));

            var key = (tenantId, workerId);
            var now = DateTime.UtcNow;

            // Check if we have a cached kernel
            if (_kernelCache.TryGetValue(key, out var cachedEntry))
            {
                // Cache hit - update last used and return cached kernel
                _kernelCache[key] = (cachedEntry.kernel, now);
                _logger?.LogDebug("Kernel cache hit for Tenant='{tenant}', Worker='{worker}'", tenantId, workerId);
                return cachedEntry.kernel;
            }

            _logger?.LogInformation("Kernel cache miss for Tenant='{tenant}', Worker='{worker}'; creating new Kernel.", tenantId, workerId);

            // Cache miss - create new kernel and apply governance if provided
            var kernel = CreateGovernedKernel(tenantId, workerId, onCacheMiss);
            _kernelCache[key] = (kernel, now);
            return kernel;
        }


        // Delegate for expensive post-creation actions (like governance)
        private Func<Kernel, Task>? _onKernelCreatedAsync;

        /// <summary>
        /// Sets the delegate to be invoked asynchronously after a kernel is created.
        /// This delegate can be used for expensive post-creation actions, such as applying governance.
        /// </summary>
        /// <param name="onKernelCreatedAsync">A delegate to execute after kernel creation.</param>
        public void SetOnKernelCreatedAsync(Func<Kernel, Task> onKernelCreatedAsync)
        {
            _onKernelCreatedAsync = onKernelCreatedAsync;
        }

        /// <summary>
        /// Sets the template kernel to copy plugins/functions from.
        /// </summary>
        public void SetTemplateKernel(Kernel kernel)
        {
            _templateKernel = kernel;
        }

        /// <summary>
        /// Creates a new kernel instance for a tenant/worker, copying plugins/functions from the template kernel.
        /// </summary>
        private Kernel CreateGovernedKernel(string tenantId, string workerId, Func<Kernel, Task>? onCacheMiss = null)
        {
            // Start with a proper kernel builder
            var kernelBuilder = Kernel.CreateBuilder();

            // Configure the chat completion service
            _configureKernelBuilder?.Invoke(kernelBuilder);

            // Build the kernel with services
            var kernel = kernelBuilder.Build();

            // Copy plugins from template kernel using the same pattern
            if (_templateKernel != null)
            {
                foreach (var plugin in _templateKernel.Plugins)
                {
                    if (plugin.FunctionCount > 0)
                    {
                        try
                        {
                            // Extract the functions from the plugin and add them
                            kernel.Plugins.AddFromFunctions(plugin.Name, plugin.AsEnumerable());
                        }
                        catch (ArgumentException)
                        {
                            // Skip plugins that can't be added
                            continue;
                        }
                    }
                }
            }

            // Apply expensive governance only on cache miss (new kernel creation)
            if (onCacheMiss != null)
            {
                // Run governance synchronously in kernel creation context
                // This ensures governance is applied before the kernel is cached and used
                onCacheMiss(kernel).GetAwaiter().GetResult();
            }
            else if (_onKernelCreatedAsync != null)
            {
                // Fallback to global delegate if no specific one provided
                _onKernelCreatedAsync(kernel).GetAwaiter().GetResult();
            }

            return kernel;
        }

        private void EvictExpiredKernels(object? state)
        {
            var now = DateTime.UtcNow;
            var evicted = 0;
            foreach (var kvp in _kernelCache)
            {
                if (now - kvp.Value.lastUsed > _cacheExpiry)
                {
                    if (_kernelCache.TryRemove(kvp.Key, out var removed))
                    {
                        evicted++;
                        // If kernel or its plugins need disposal, handle here
                        // No-op: Kernel does not implement IDisposable
                    }
                }
            }

            if (evicted > 0)
            {
                _logger?.LogInformation("Evicted {count} expired kernel(s) from cache.", evicted);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _evictionTimer.Dispose();
            // If kernel or its plugins need disposal, handle here
            // No-op: Kernel does not implement IDisposable
            _disposed = true;
        }

        // Minimal stub for IKernelBuilderPlugins to satisfy interface
        private class KernelBuilderPluginsStub : IKernelBuilderPlugins
        {
            public IServiceCollection Services => new ServiceCollection();
        }

        private class KernelBuilderWrapper : IKernelBuilder
        {
            private readonly Kernel _kernel;
            public KernelBuilderWrapper(Kernel kernel) => _kernel = kernel;
            public Kernel Build() => _kernel;
            public IKernelBuilderPlugins Plugins => new KernelBuilderPluginsStub();
            public IServiceCollection Services => new ServiceCollection();
        }
    }
}