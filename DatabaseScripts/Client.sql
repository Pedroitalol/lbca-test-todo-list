CREATE TABLE [Client] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Email] nvarchar(450) NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [FirebaseUid] nvarchar(256) NULL,
    [AuthProvider] nvarchar(64) NULL,
    [StripeCustomerId] nvarchar(128) NULL,
    [StripeSubscriptionId] nvarchar(128) NULL,
    [StripePriceId] nvarchar(128) NULL,
    [StripeSubscriptionStatus] nvarchar(64) NULL,
    [StripePendingPaymentIntentId] nvarchar(128) NULL,
    [StripePendingPaymentIntentCreatedAtUtc] datetime2 NULL,
    [StripeHostedInvoiceUrl] nvarchar(2048) NULL,
    [Plan] int NOT NULL,
    [Role] int NOT NULL DEFAULT 0,
    [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit),
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Client] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE INDEX [Index_AuthEmail] ON [Client] ([Email]);
GO

CREATE UNIQUE INDEX [Index_FirebaseUid] ON [Client] ([FirebaseUid]) WHERE "FirebaseUid" IS NOT NULL;
GO

CREATE INDEX [Index_StripeCustomerId] ON [Client] ([StripeCustomerId]) WHERE "StripeCustomerId" IS NOT NULL;
GO
