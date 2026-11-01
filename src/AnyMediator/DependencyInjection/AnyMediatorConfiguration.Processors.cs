using Microsoft.Extensions.DependencyInjection;

namespace AnyMediator;

public sealed partial class AnyMediatorConfiguration
{
    // ---------- Request pre-processors ----------

    /// <summary>
    /// Explicitly registers a single <see cref="IRequestPreProcessor{TRequest}"/> implementation.
    /// The pre-processor wrapper behavior is wired automatically.
    /// </summary>
    public AnyMediatorConfiguration AddRequestPreProcessor<TPreProcessor>(ServiceLifetime? serviceLifetime = null)
        where TPreProcessor : class
        => AddRequestPreProcessor(typeof(TPreProcessor), serviceLifetime);

    /// <inheritdoc cref="AddRequestPreProcessor{TPreProcessor}"/>
    public AnyMediatorConfiguration AddRequestPreProcessor(Type preProcessorType, ServiceLifetime? serviceLifetime = null)
        => AddClosedFromImplementation(
            RequestPreProcessorsToRegister, typeof(IRequestPreProcessor<>), preProcessorType, serviceLifetime);

    /// <summary>
    /// Registers a closed pre-processor under an explicit
    /// <see cref="IRequestPreProcessor{TRequest}"/> service type.
    /// </summary>
    public AnyMediatorConfiguration AddRequestPreProcessor<TServiceType, TImplementationType>(
        ServiceLifetime? serviceLifetime = null)
        where TServiceType : class
        where TImplementationType : class, TServiceType
        => AddRequestPreProcessor(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <inheritdoc cref="AddRequestPreProcessor{TServiceType, TImplementationType}"/>
    public AnyMediatorConfiguration AddRequestPreProcessor(
        Type serviceType, Type implementationType, ServiceLifetime? serviceLifetime = null)
        => AddClosedWithExplicitService(
            RequestPreProcessorsToRegister, typeof(IRequestPreProcessor<>), serviceType, implementationType, serviceLifetime);

    /// <summary>
    /// Registers an open generic pre-processor (e.g. <c>LoggingPreProcessor&lt;TRequest&gt;</c>) that runs
    /// before every request handler.
    /// </summary>
    public AnyMediatorConfiguration AddOpenRequestPreProcessor(
        Type openPreProcessorType, ServiceLifetime? serviceLifetime = null)
        => AddOpenGenericRegistration(
            RequestPreProcessorsToRegister, typeof(IRequestPreProcessor<>), openPreProcessorType, serviceLifetime);

    // ---------- Request post-processors ----------

    /// <summary>
    /// Explicitly registers a single <see cref="IRequestPostProcessor{TRequest, TResponse}"/> implementation.
    /// The post-processor wrapper behavior is wired automatically.
    /// </summary>
    public AnyMediatorConfiguration AddRequestPostProcessor<TPostProcessor>(ServiceLifetime? serviceLifetime = null)
        where TPostProcessor : class
        => AddRequestPostProcessor(typeof(TPostProcessor), serviceLifetime);

    /// <inheritdoc cref="AddRequestPostProcessor{TPostProcessor}"/>
    public AnyMediatorConfiguration AddRequestPostProcessor(Type postProcessorType, ServiceLifetime? serviceLifetime = null)
        => AddClosedFromImplementation(
            RequestPostProcessorsToRegister, typeof(IRequestPostProcessor<,>), postProcessorType, serviceLifetime);

    /// <summary>
    /// Registers a closed post-processor under an explicit
    /// <see cref="IRequestPostProcessor{TRequest, TResponse}"/> service type.
    /// </summary>
    public AnyMediatorConfiguration AddRequestPostProcessor<TServiceType, TImplementationType>(
        ServiceLifetime? serviceLifetime = null)
        where TServiceType : class
        where TImplementationType : class, TServiceType
        => AddRequestPostProcessor(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <inheritdoc cref="AddRequestPostProcessor{TServiceType, TImplementationType}"/>
    public AnyMediatorConfiguration AddRequestPostProcessor(
        Type serviceType, Type implementationType, ServiceLifetime? serviceLifetime = null)
        => AddClosedWithExplicitService(
            RequestPostProcessorsToRegister, typeof(IRequestPostProcessor<,>), serviceType, implementationType, serviceLifetime);

    /// <summary>
    /// Registers an open generic post-processor (e.g. <c>AuditPostProcessor&lt;TRequest, TResponse&gt;</c>)
    /// that runs after every successful request handler.
    /// </summary>
    public AnyMediatorConfiguration AddOpenRequestPostProcessor(
        Type openPostProcessorType, ServiceLifetime? serviceLifetime = null)
        => AddOpenGenericRegistration(
            RequestPostProcessorsToRegister, typeof(IRequestPostProcessor<,>), openPostProcessorType, serviceLifetime);

    // ---------- Request exception handlers ----------

    /// <summary>
    /// Explicitly registers an <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>
    /// implementation. The exception-handler wrapper behavior is wired automatically.
    /// </summary>
    public AnyMediatorConfiguration AddRequestExceptionHandler<TExceptionHandler>(ServiceLifetime? serviceLifetime = null)
        where TExceptionHandler : class
        => AddRequestExceptionHandler(typeof(TExceptionHandler), serviceLifetime);

    /// <inheritdoc cref="AddRequestExceptionHandler{TExceptionHandler}"/>
    public AnyMediatorConfiguration AddRequestExceptionHandler(
        Type exceptionHandlerType, ServiceLifetime? serviceLifetime = null)
        => AddClosedFromImplementation(
            RequestExceptionHandlersToRegister, typeof(IRequestExceptionHandler<,,>), exceptionHandlerType, serviceLifetime);

    // ---------- Request exception actions ----------

    /// <summary>
    /// Explicitly registers an <see cref="IRequestExceptionAction{TRequest, TException}"/> implementation.
    /// The exception-action wrapper behavior is wired automatically.
    /// </summary>
    public AnyMediatorConfiguration AddRequestExceptionAction<TExceptionAction>(ServiceLifetime? serviceLifetime = null)
        where TExceptionAction : class
        => AddRequestExceptionAction(typeof(TExceptionAction), serviceLifetime);

    /// <inheritdoc cref="AddRequestExceptionAction{TExceptionAction}"/>
    public AnyMediatorConfiguration AddRequestExceptionAction(
        Type exceptionActionType, ServiceLifetime? serviceLifetime = null)
        => AddClosedFromImplementation(
            RequestExceptionActionsToRegister, typeof(IRequestExceptionAction<,>), exceptionActionType, serviceLifetime);
}
