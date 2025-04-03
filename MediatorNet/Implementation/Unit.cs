namespace MediatorNet.Implementation;

/// <summary>
/// Represents a void type, since void isn't a valid return type in C#
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// The single Unit value
    /// </summary>
    public static readonly Unit Value = new();

    /// <summary>
    /// Compares the current Unit with another Unit
    /// </summary>
    /// <param name="other">Instance to compare with</param>
    /// <returns>Always true since Unit is a singleton type</returns>
    public bool Equals(Unit other) => true;

    /// <summary>
    /// Compares the current Unit with another object
    /// </summary>
    /// <param name="obj">Object to compare with</param>
    /// <returns>True if obj is Unit</returns>
    public override bool Equals(object? obj) => obj is Unit;

    /// <summary>
    /// Returns a hash code for this instance
    /// </summary>
    /// <returns>A hash code for Unit</returns>
    public override int GetHashCode() => 0;

    /// <summary>
    /// Returns a string representation of the Unit
    /// </summary>
    /// <returns>"()"</returns>
    public override string ToString() => "()";

    /// <summary>
    /// Compares two Units for equality
    /// </summary>
    /// <param name="left">First Unit instance</param>
    /// <param name="right">Second Unit instance</param>
    /// <returns>Always true</returns>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Compares two Units for inequality
    /// </summary>
    /// <param name="left">First Unit instance</param>
    /// <param name="right">Second Unit instance</param>
    /// <returns>Always false</returns>
    public static bool operator !=(Unit left, Unit right) => false;

    /// <summary>
    /// Creates a ValueTask containing a Unit value
    /// </summary>
    /// <returns>A ValueTask containing Unit.Value</returns>
    public static ValueTask<Unit> ValueTask() => new(Value);
}