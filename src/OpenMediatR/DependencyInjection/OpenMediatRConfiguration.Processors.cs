using Microsoft.Extensions.DependencyInjection;

namespace OpenMediatR;

public sealed partial class OpenMediatRConfiguration
{
    // ---------- Request pre-processors ----------

    /// <summary>
    /// Explicitly registers a single <see cref="IRequestPreProcessor{TRequest}"/> implementation.
    /// The pre-processor wrapper behavior is wired automatically.
    /// </summary>
    public OpenMediatRConfiguration AddRequestPreProcessor<TPreProcessor>(ServiceLifetime? serviceLifetime = null)
        where TPreProcessor : class
        => AddRequestPreProcessor(typeof(TPreProcessor), serviceLifetime);

    public OpenMediatRConfiguration AddRequestPreProcessor(Type preProcessorType, ServiceLifetime? serviceLifetime = null)
    {
        AddClosedFromImplementation(
            RequestPreProcessorsToRegister, typeof(IRequestPreProcessor<>), preProcessorType, serviceLifetime);
        return this;
    }

    /// <summary>
    /// Registers a closed pre-processor under an explicit
    /// <see cref="IRequestPreProcessor{TRequest}"/> service type.
    /// </summary>
    public OpenMediatRConfiguration AddRequestPreProcessor<TServiceType, TImplementationType>(
        ServiceLifetime? serviceLifetime = null)
        where TServiceType : class
        where TImplementationType : class, TServiceType
        => AddRequestPreProcessor(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    public OpenMediatRConfiguration AddRequestPreProcessor(
        Type serviceType, Type implementationType, ServiceLifetime? serviceLifetime = null)
    {
        AddClosedWithExplicitService(
            RequestPreProcessorsToRegister, typeof(IRequestPreProcessor<>), serviceType, implementationType, serviceLifetime);
        return this;
    }

    /// <summary>
    /// Registers an open generic pre-processor (e.g. <c>LoggingPreProcessor&lt;TRequest&gt;</c>) that runs
    /// before every request handler.
    /// </summary>
    public OpenMediatRConfiguration AddOpenRequestPreProcessor(
        Type openPreProcessorType, ServiceLifetime? serviceLifetime = null)
    {
        AddOpenGenericRegistration(
            RequestPreProcessorsToRegister, typeof(IRequestPreProcessor<>), openPreProcessorType, serviceLifetime);
        return this;
    }

    // ---------- Request post-processors ----------

    /// <summary>
    /// Explicitly registers a single <see cref="IRequestPostProcessor{TRequest, TResponse}"/> implementation.
    /// The post-processor wrapper behavior is wired automatically.
    /// </summary>
    public OpenMediatRConfiguration AddRequestPostProcessor<TPostProcessor>(ServiceLifetime? serviceLifetime = null)
        where TPostProcessor : class
        => AddRequestPostProcessor(typeof(TPostProcessor), serviceLifetime);

    public OpenMediatRConfiguration AddRequestPostProcessor(Type postProcessorType, ServiceLifetime? serviceLifetime = null)
    {
        AddClosedFromImplementation(
            RequestPostProcessorsToRegister, typeof(IRequestPostProcessor<,>), postProcessorType, serviceLifetime);
        return this;
    }

    /// <summary>
    /// Registers a closed post-processor under an explicit
    /// <see cref="IRequestPostProcessor{TRequest, TResponse}"/> service type.
    /// </summary>
    public OpenMediatRConfiguration AddRequestPostProcessor<TServiceType, TImplementationType>(
        ServiceLifetime? serviceLifetime = null)
        where TServiceType : class
        where TImplementationType : class, TServiceType
        => AddRequestPostProcessor(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    public OpenMediatRConfiguration AddRequestPostProcessor(
        Type serviceType, Type implementationType, ServiceLifetime? serviceLifetime = null)
    {
        AddClosedWithExplicitService(
            RequestPostProcessorsToRegister, typeof(IRequestPostProcessor<,>), serviceType, implementationType, serviceLifetime);
        return this;
    }

    /// <summary>
    /// Registers an open generic post-processor (e.g. <c>AuditPostProcessor&lt;TRequest, TResponse&gt;</c>)
    /// that runs after every successful request handler.
    /// </summary>
    public OpenMediatRConfiguration AddOpenRequestPostProcessor(
        Type openPostProcessorType, ServiceLifetime? serviceLifetime = null)
    {
        AddOpenGenericRegistration(
            RequestPostProcessorsToRegister, typeof(IRequestPostProcessor<,>), openPostProcessorType, serviceLifetime);
        return this;
    }

    // ---------- Request exception handlers ----------

    /// <summary>
    /// Explicitly registers an <see cref="IRequestExceptionHandler{TRequest, TResponse, TException}"/>
    /// implementation. The exception-handler wrapper behavior is wired automatically.
    /// </summary>
    public OpenMediatRConfiguration AddRequestExceptionHandler<TExceptionHandler>(ServiceLifetime? serviceLifetime = null)
        where TExceptionHandler : class
        => AddRequestExceptionHandler(typeof(TExceptionHandler), serviceLifetime);

    public OpenMediatRConfiguration AddRequestExceptionHandler(
        Type exceptionHandlerType, ServiceLifetime? serviceLifetime = null)
    {
        AddClosedFromImplementation(
            RequestExceptionHandlersToRegister, typeof(IRequestExceptionHandler<,,>), exceptionHandlerType, serviceLifetime);
        return this;
    }

    // ---------- Request exception actions ----------

    /// <summary>
    /// Explicitly registers an <see cref="IRequestExceptionAction{TRequest, TException}"/> implementation.
    /// The exception-action wrapper behavior is wired automatically.
    /// </summary>
    public OpenMediatRConfiguration AddRequestExceptionAction<TExceptionAction>(ServiceLifetime? serviceLifetime = null)
        where TExceptionAction : class
        => AddRequestExceptionAction(typeof(TExceptionAction), serviceLifetime);

    public OpenMediatRConfiguration AddRequestExceptionAction(
        Type exceptionActionType, ServiceLifetime? serviceLifetime = null)
    {
        AddClosedFromImplementation(
            RequestExceptionActionsToRegister, typeof(IRequestExceptionAction<,>), exceptionActionType, serviceLifetime);
        return this;
    }
}
