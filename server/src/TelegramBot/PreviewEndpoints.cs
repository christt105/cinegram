namespace Cinegram.TelegramBot;

/// <summary>Sends a collection or a whole season to the owner's Telegram chat as a preview.</summary>
public static class PreviewEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        // Trigger preview of a single movie collection (info card + files)
        app.MapPost("/preview/collection/{collectionId:int}", async (int collectionId, IServiceProvider sp) =>
        {
            var preview = ActivatorUtilities.CreateInstance<PreviewService>(sp);
            var (ok, error) = await preview.SendCollectionPreviewAsync(collectionId);
            return ok ? Results.Ok(new { status = "ok" }) : Results.Problem(error);
        });

        // Trigger preview of all files in a season
        app.MapPost("/preview/series/{seriesId:int}/season/{seasonNumber:int}", async (int seriesId, int seasonNumber, IServiceProvider sp) =>
        {
            var preview = ActivatorUtilities.CreateInstance<PreviewService>(sp);
            var (ok, error) = await preview.SendSeasonPreviewAsync(seriesId, seasonNumber);
            return ok ? Results.Ok(new { status = "ok" }) : Results.Problem(error);
        });
    }
}
