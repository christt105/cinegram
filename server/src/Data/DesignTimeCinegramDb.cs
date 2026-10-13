using Microsoft.EntityFrameworkCore.Design;

namespace Cinegram.Data;

/// <summary>Lets <c>dotnet ef</c> build the model without starting the bot.</summary>
public class DesignTimeCinegramDb : IDesignTimeDbContextFactory<CinegramDb>
{
    public CinegramDb CreateDbContext(string[] args) => new(CinegramDb.Options("design-time.db"));
}
