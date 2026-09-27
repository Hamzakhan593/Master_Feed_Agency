using Master_Feed_Agency.Data;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Services;

public static class WhatsAppSchemaInitializer
{
    public static async Task EnsureAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        const string sql = """
IF OBJECT_ID(N'[dbo].[WhatsAppMessages]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WhatsAppMessages]
    (
        [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_WhatsAppMessages] PRIMARY KEY,
        [CustomerId] int NULL,
        [SaleId] bigint NULL,
        [PaymentId] bigint NULL,
        [Kind] int NOT NULL,
        [Recipient] nvarchar(30) NOT NULL,
        [TemplateName] nvarchar(150) NULL,
        [MessagePreview] nvarchar(2000) NOT NULL,
        [Status] int NOT NULL,
        [ProviderReference] nvarchar(200) NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [IsAutomatic] bit NOT NULL,
        [DeduplicationKey] nvarchar(180) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [AttemptedAt] datetime2 NULL,
        [SentAt] datetime2 NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        CONSTRAINT [FK_WhatsAppMessages_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers]([Id]),
        CONSTRAINT [FK_WhatsAppMessages_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [dbo].[Sales]([Id]),
        CONSTRAINT [FK_WhatsAppMessages_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [dbo].[Payments]([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WhatsAppMessages_DeduplicationKey' AND object_id = OBJECT_ID(N'[dbo].[WhatsAppMessages]'))
    CREATE UNIQUE INDEX [IX_WhatsAppMessages_DeduplicationKey] ON [dbo].[WhatsAppMessages]([DeduplicationKey]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WhatsAppMessages_CustomerId_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[WhatsAppMessages]'))
    CREATE INDEX [IX_WhatsAppMessages_CustomerId_CreatedAt] ON [dbo].[WhatsAppMessages]([CustomerId], [CreatedAt]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WhatsAppMessages_Status_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[WhatsAppMessages]'))
    CREATE INDEX [IX_WhatsAppMessages_Status_CreatedAt] ON [dbo].[WhatsAppMessages]([Status], [CreatedAt]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WhatsAppMessages_PaymentId' AND object_id = OBJECT_ID(N'[dbo].[WhatsAppMessages]'))
    CREATE INDEX [IX_WhatsAppMessages_PaymentId] ON [dbo].[WhatsAppMessages]([PaymentId]);
""";

        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not initialize WhatsApp message storage.");
            throw;
        }
    }
}
