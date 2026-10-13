using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartTrip.Infrastructure.Data;

public sealed class DesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options =
            new DbContextOptionsBuilder<ApplicationDbContext>();

        string connection =
            Environment.GetEnvironmentVariable(
                "SMARTTRIP_CONNECTION")
            ?? "Host=localhost;Database=SmartTripDb;Username=postgres";

        options.UseNpgsql(connection);

        return new ApplicationDbContext(options.Options);
    }
}