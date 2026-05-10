using Microsoft.Extensions.DependencyInjection;

namespace OpenMediatR;

/// <summary>
/// Open-generic pipeline behavior registration entry. Pairs the behavior type with an optional
/// per-item lifetime override; when <see cref="Lifetime"/> is <c>null</c> the configuration's
/// default lifetime is used.
/// </summary>
public readonly record struct OpenBehavior(Type OpenBehaviorType, ServiceLifetime? Lifetime = null);
