namespace BytePulse.Core.Native;

/// <summary>
/// Enumerates the entries of one directory.
/// </summary>
/// <remarks>
/// The entry that <see cref="Current"/> returns is only valid until the next call to
/// <see cref="MoveNext"/>, because the name is read directly out of the enumerator's
/// buffer rather than copied.
/// </remarks>
public abstract class DirectoryEnumerator : IDisposable
{
    /// <summary>Advances to the next entry.</summary>
    /// <returns>False when the directory has no more entries.</returns>
    public abstract bool MoveNext();

    /// <summary>The entry the enumerator is positioned on.</summary>
    public abstract Scanning.DirectoryEntry Current { get; }

    /// <summary>Releases the enumerator's resources.</summary>
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
