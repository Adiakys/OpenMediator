namespace OpenMediatR;

/// <summary>
/// Represents the absence of a value, used as the response type of requests that return nothing
/// (<see cref="IRequest"/>). All instances are equal.
/// </summary>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>
{
    /// <summary>
    /// The single <see cref="Unit"/> value.
    /// </summary>
    public static readonly Unit Value = default;

    /// <summary>
    /// A completed task whose result is <see cref="Value"/>.
    /// </summary>
    public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);

    /// <inheritdoc />
    public int CompareTo(Unit other) => 0;

    /// <inheritdoc />
    public bool Equals(Unit other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <summary>
    /// Returns <c>()</c>.
    /// </summary>
    public override string ToString() => "()";

    /// <summary>
    /// Always <see langword="true"/>: all <see cref="Unit"/> values are equal.
    /// </summary>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Always <see langword="false"/>: all <see cref="Unit"/> values are equal.
    /// </summary>
    public static bool operator !=(Unit left, Unit right) => false;
}
