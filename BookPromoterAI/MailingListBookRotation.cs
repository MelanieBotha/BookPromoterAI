namespace BookPromoterAI;

static class MailingListBookRotation
{
    /// <summary>Picks the next book in stable Id order, rotating through the author's library.</summary>
    public static T? PickBook<T>(IReadOnlyList<T> books, Func<T, int> idSelector, int? explicitBookId, int? lastBookId, bool advanceBook)
        where T : class
    {
        if (books.Count == 0) return null;
        var ordered = books.OrderBy(idSelector).ToList();

        if (explicitBookId is int id)
        {
            var idx = ordered.FindIndex(b => idSelector(b) == id);
            if (idx < 0) return ordered[0];
            return advanceBook ? ordered[(idx + 1) % ordered.Count] : ordered[idx];
        }

        if (lastBookId is int lastId)
        {
            var idx = ordered.FindIndex(b => idSelector(b) == lastId);
            if (idx >= 0)
                return ordered[(idx + 1) % ordered.Count];
        }

        return ordered[0];
    }
}
