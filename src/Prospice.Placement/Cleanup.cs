namespace Prospice.Placement;

// Attempt every restoration even if an earlier Unity object has become invalid.
internal static class Cleanup
{
    internal static IReadOnlyList<Exception> Run(params Action[] actions)
    {
        var errors = new List<Exception>();
        foreach (var action in actions)
        {
            try { action(); }
            catch (Exception error) { errors.Add(error); }
        }
        return errors;
    }
}
