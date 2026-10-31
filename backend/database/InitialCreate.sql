IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [accounts] (
        [id] bigint NOT NULL IDENTITY,
        [phone] varchar(20) NOT NULL,
        [display_name] nvarchar(100) NOT NULL,
        [password_hash] varchar(255) NOT NULL,
        [role] varchar(30) NOT NULL,
        [must_change_password] bit NOT NULL DEFAULT CAST(0 AS bit),
        [is_active] bit NOT NULL DEFAULT CAST(1 AS bit),
        [created_by_account_id] bigint NULL,
        [created_at] datetimeoffset NOT NULL,
        CONSTRAINT [PK_accounts] PRIMARY KEY ([id]),
        CONSTRAINT [FK_accounts_accounts_created_by_account_id] FOREIGN KEY ([created_by_account_id]) REFERENCES [accounts] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [computer_types] (
        [id] bigint NOT NULL IDENTITY,
        [name] varchar(30) NOT NULL,
        [hourly_rate] decimal(18,0) NOT NULL,
        CONSTRAINT [PK_computer_types] PRIMARY KEY ([id]),
        CONSTRAINT [CK_computer_types_rate] CHECK ([hourly_rate] > 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [product_categories] (
        [id] bigint NOT NULL IDENTITY,
        [name] nvarchar(80) NOT NULL,
        CONSTRAINT [PK_product_categories] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [customers] (
        [id] bigint NOT NULL IDENTITY,
        [account_id] bigint NOT NULL,
        [wallet_balance] decimal(18,0) NOT NULL DEFAULT 0.0,
        [wallet_version] bigint NOT NULL DEFAULT CAST(0 AS bigint),
        CONSTRAINT [PK_customers] PRIMARY KEY ([id]),
        CONSTRAINT [CK_customers_wallet_balance] CHECK ([wallet_balance] >= 0),
        CONSTRAINT [FK_customers_accounts_account_id] FOREIGN KEY ([account_id]) REFERENCES [accounts] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [tournaments] (
        [id] bigint NOT NULL IDENTITY,
        [name] nvarchar(150) NOT NULL,
        [registration_closes_at] datetimeoffset NOT NULL,
        [status] varchar(30) NOT NULL DEFAULT 'Open',
        [created_by_account_id] bigint NOT NULL,
        [created_at] datetimeoffset NOT NULL,
        [closed_at] datetimeoffset NULL,
        [teams_finalized_at] datetimeoffset NULL,
        [cancelled_by_account_id] bigint NULL,
        [cancelled_at] datetimeoffset NULL,
        CONSTRAINT [PK_tournaments] PRIMARY KEY ([id]),
        CONSTRAINT [FK_tournaments_accounts_cancelled_by_account_id] FOREIGN KEY ([cancelled_by_account_id]) REFERENCES [accounts] ([id]),
        CONSTRAINT [FK_tournaments_accounts_created_by_account_id] FOREIGN KEY ([created_by_account_id]) REFERENCES [accounts] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [computers] (
        [id] bigint NOT NULL IDENTITY,
        [code] varchar(30) NOT NULL,
        [computer_type_id] bigint NOT NULL,
        [is_under_maintenance] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_computers] PRIMARY KEY ([id]),
        CONSTRAINT [FK_computers_computer_types_computer_type_id] FOREIGN KEY ([computer_type_id]) REFERENCES [computer_types] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [products] (
        [id] bigint NOT NULL IDENTITY,
        [category_id] bigint NOT NULL,
        [name] nvarchar(120) NOT NULL,
        [current_price] decimal(18,0) NOT NULL,
        CONSTRAINT [PK_products] PRIMARY KEY ([id]),
        CONSTRAINT [CK_products_price] CHECK ([current_price] >= 0),
        CONSTRAINT [FK_products_product_categories_category_id] FOREIGN KEY ([category_id]) REFERENCES [product_categories] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [top_ups] (
        [id] bigint NOT NULL IDENTITY,
        [customer_id] bigint NOT NULL,
        [amount] decimal(18,0) NOT NULL,
        [requested_by_customer] bit NOT NULL DEFAULT CAST(0 AS bit),
        [status] varchar(30) NOT NULL,
        [requested_at] datetimeoffset NOT NULL,
        [credited_by_account_id] bigint NULL,
        [credited_at] datetimeoffset NULL,
        [collected_by_account_id] bigint NULL,
        [collected_at] datetimeoffset NULL,
        [collection_method] varchar(30) NULL,
        CONSTRAINT [PK_top_ups] PRIMARY KEY ([id]),
        CONSTRAINT [CK_top_ups_amount] CHECK ([amount] > 0),
        CONSTRAINT [FK_top_ups_accounts_collected_by_account_id] FOREIGN KEY ([collected_by_account_id]) REFERENCES [accounts] ([id]),
        CONSTRAINT [FK_top_ups_accounts_credited_by_account_id] FOREIGN KEY ([credited_by_account_id]) REFERENCES [accounts] ([id]),
        CONSTRAINT [FK_top_ups_customers_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [team_generation_runs] (
        [id] bigint NOT NULL IDENTITY,
        [tournament_id] bigint NOT NULL,
        [model_name] varchar(100) NOT NULL,
        [model_version] varchar(50) NOT NULL,
        [generated_by_account_id] bigint NOT NULL,
        [generated_at] datetimeoffset NOT NULL,
        CONSTRAINT [PK_team_generation_runs] PRIMARY KEY ([id]),
        CONSTRAINT [FK_team_generation_runs_accounts_generated_by_account_id] FOREIGN KEY ([generated_by_account_id]) REFERENCES [accounts] ([id]),
        CONSTRAINT [FK_team_generation_runs_tournaments_tournament_id] FOREIGN KEY ([tournament_id]) REFERENCES [tournaments] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [teams] (
        [id] bigint NOT NULL IDENTITY,
        [tournament_id] bigint NOT NULL,
        [team_number] int NOT NULL,
        [finalized_by_account_id] bigint NOT NULL,
        [finalized_at] datetimeoffset NOT NULL,
        CONSTRAINT [PK_teams] PRIMARY KEY ([id]),
        CONSTRAINT [CK_teams_number] CHECK ([team_number] > 0),
        CONSTRAINT [FK_teams_accounts_finalized_by_account_id] FOREIGN KEY ([finalized_by_account_id]) REFERENCES [accounts] ([id]),
        CONSTRAINT [FK_teams_tournaments_tournament_id] FOREIGN KEY ([tournament_id]) REFERENCES [tournaments] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [tournament_registrations] (
        [id] bigint NOT NULL IDENTITY,
        [tournament_id] bigint NOT NULL,
        [customer_id] bigint NOT NULL,
        [riot_game_name] nvarchar(100) NOT NULL,
        [riot_tagline] varchar(20) NOT NULL,
        [fee_amount_snapshot] decimal(18,0) NOT NULL DEFAULT 50000.0,
        [fee_status] varchar(30) NOT NULL DEFAULT 'Paid',
        [registered_at] datetimeoffset NOT NULL,
        [refunded_at] datetimeoffset NULL,
        [refund_reason] varchar(30) NULL,
        CONSTRAINT [PK_tournament_registrations] PRIMARY KEY ([id]),
        CONSTRAINT [CK_tournament_registrations_fee] CHECK ([fee_amount_snapshot] > 0),
        CONSTRAINT [FK_tournament_registrations_customers_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]),
        CONSTRAINT [FK_tournament_registrations_tournaments_tournament_id] FOREIGN KEY ([tournament_id]) REFERENCES [tournaments] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [gaming_sessions] (
        [id] bigint NOT NULL IDENTITY,
        [customer_id] bigint NOT NULL,
        [computer_id] bigint NOT NULL,
        [hourly_rate_snapshot] decimal(18,0) NOT NULL,
        [started_at] datetimeoffset NOT NULL,
        [last_charged_at] datetimeoffset NOT NULL,
        [charged_amount] decimal(18,0) NOT NULL DEFAULT 0.0,
        [status] varchar(30) NOT NULL,
        [ended_at] datetimeoffset NULL,
        [end_reason] varchar(30) NULL,
        [ended_by_account_id] bigint NULL,
        CONSTRAINT [PK_gaming_sessions] PRIMARY KEY ([id]),
        CONSTRAINT [CK_gaming_sessions_amounts] CHECK ([hourly_rate_snapshot] > 0 AND [charged_amount] >= 0),
        CONSTRAINT [CK_gaming_sessions_time] CHECK ([last_charged_at] >= [started_at] AND ([ended_at] IS NULL OR [ended_at] >= [started_at])),
        CONSTRAINT [FK_gaming_sessions_accounts_ended_by_account_id] FOREIGN KEY ([ended_by_account_id]) REFERENCES [accounts] ([id]),
        CONSTRAINT [FK_gaming_sessions_computers_computer_id] FOREIGN KEY ([computer_id]) REFERENCES [computers] ([id]),
        CONSTRAINT [FK_gaming_sessions_customers_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [player_stats_snapshots] (
        [id] bigint NOT NULL IDENTITY,
        [tournament_registration_id] bigint NOT NULL,
        [status] varchar(30) NOT NULL,
        [source] varchar(30) NULL,
        [scope] varchar(80) NULL,
        [matches] int NULL,
        [win_rate] decimal(5,2) NULL,
        [headshot_rate] decimal(5,2) NULL,
        [damage_per_round] decimal(10,2) NULL,
        [kda] decimal(10,2) NULL,
        [fetched_at] datetimeoffset NULL,
        [source_updated_at] datetimeoffset NULL,
        CONSTRAINT [PK_player_stats_snapshots] PRIMARY KEY ([id]),
        CONSTRAINT [FK_player_stats_snapshots_tournament_registrations_tournament_registration_id] FOREIGN KEY ([tournament_registration_id]) REFERENCES [tournament_registrations] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [team_members] (
        [id] bigint NOT NULL IDENTITY,
        [team_id] bigint NOT NULL,
        [tournament_registration_id] bigint NOT NULL,
        CONSTRAINT [PK_team_members] PRIMARY KEY ([id]),
        CONSTRAINT [FK_team_members_teams_team_id] FOREIGN KEY ([team_id]) REFERENCES [teams] ([id]),
        CONSTRAINT [FK_team_members_tournament_registrations_tournament_registration_id] FOREIGN KEY ([tournament_registration_id]) REFERENCES [tournament_registrations] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [team_proposals] (
        [id] bigint NOT NULL IDENTITY,
        [generation_run_id] bigint NOT NULL,
        [tournament_registration_id] bigint NOT NULL,
        [proposed_team_number] int NOT NULL,
        [predicted_skill_score] decimal(10,4) NULL,
        CONSTRAINT [PK_team_proposals] PRIMARY KEY ([id]),
        CONSTRAINT [CK_team_proposals_number] CHECK ([proposed_team_number] > 0),
        CONSTRAINT [FK_team_proposals_team_generation_runs_generation_run_id] FOREIGN KEY ([generation_run_id]) REFERENCES [team_generation_runs] ([id]),
        CONSTRAINT [FK_team_proposals_tournament_registrations_tournament_registration_id] FOREIGN KEY ([tournament_registration_id]) REFERENCES [tournament_registrations] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [orders] (
        [id] bigint NOT NULL IDENTITY,
        [customer_id] bigint NOT NULL,
        [gaming_session_id] bigint NOT NULL,
        [total_amount] decimal(18,0) NOT NULL,
        [placed_at] datetimeoffset NOT NULL,
        CONSTRAINT [PK_orders] PRIMARY KEY ([id]),
        CONSTRAINT [CK_orders_total] CHECK ([total_amount] >= 0),
        CONSTRAINT [FK_orders_customers_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]),
        CONSTRAINT [FK_orders_gaming_sessions_gaming_session_id] FOREIGN KEY ([gaming_session_id]) REFERENCES [gaming_sessions] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [order_items] (
        [id] bigint NOT NULL IDENTITY,
        [order_id] bigint NOT NULL,
        [product_id] bigint NOT NULL,
        [quantity] int NOT NULL,
        [unit_price_snapshot] decimal(18,0) NOT NULL,
        [status] varchar(30) NOT NULL DEFAULT 'Pending',
        [confirmed_by_account_id] bigint NULL,
        [confirmed_at] datetimeoffset NULL,
        [cancelled_by_account_id] bigint NULL,
        [cancelled_at] datetimeoffset NULL,
        CONSTRAINT [PK_order_items] PRIMARY KEY ([id]),
        CONSTRAINT [CK_order_items_values] CHECK ([quantity] > 0 AND [unit_price_snapshot] >= 0),
        CONSTRAINT [FK_order_items_accounts_cancelled_by_account_id] FOREIGN KEY ([cancelled_by_account_id]) REFERENCES [accounts] ([id]),
        CONSTRAINT [FK_order_items_accounts_confirmed_by_account_id] FOREIGN KEY ([confirmed_by_account_id]) REFERENCES [accounts] ([id]),
        CONSTRAINT [FK_order_items_orders_order_id] FOREIGN KEY ([order_id]) REFERENCES [orders] ([id]),
        CONSTRAINT [FK_order_items_products_product_id] FOREIGN KEY ([product_id]) REFERENCES [products] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [order_item_cancel_requests] (
        [id] bigint NOT NULL IDENTITY,
        [order_item_id] bigint NOT NULL,
        [requested_by_customer_id] bigint NOT NULL,
        [status] varchar(30) NOT NULL DEFAULT 'Pending',
        [requested_at] datetimeoffset NOT NULL,
        [handled_by_account_id] bigint NULL,
        [handled_at] datetimeoffset NULL,
        CONSTRAINT [PK_order_item_cancel_requests] PRIMARY KEY ([id]),
        CONSTRAINT [FK_order_item_cancel_requests_accounts_handled_by_account_id] FOREIGN KEY ([handled_by_account_id]) REFERENCES [accounts] ([id]),
        CONSTRAINT [FK_order_item_cancel_requests_customers_requested_by_customer_id] FOREIGN KEY ([requested_by_customer_id]) REFERENCES [customers] ([id]),
        CONSTRAINT [FK_order_item_cancel_requests_order_items_order_item_id] FOREIGN KEY ([order_item_id]) REFERENCES [order_items] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE TABLE [wallet_transactions] (
        [id] bigint NOT NULL IDENTITY,
        [customer_id] bigint NOT NULL,
        [transaction_type] varchar(30) NOT NULL,
        [amount] decimal(18,0) NOT NULL,
        [balance_after] decimal(18,0) NOT NULL,
        [top_up_id] bigint NULL,
        [gaming_session_id] bigint NULL,
        [order_id] bigint NULL,
        [order_item_id] bigint NULL,
        [tournament_registration_id] bigint NULL,
        [recorded_by_account_id] bigint NULL,
        [recorded_at] datetimeoffset NOT NULL,
        CONSTRAINT [PK_wallet_transactions] PRIMARY KEY ([id]),
        CONSTRAINT [CK_wallet_transactions_balance] CHECK ([balance_after] >= 0),
        CONSTRAINT [CK_wallet_transactions_source] CHECK ((CASE WHEN [top_up_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [gaming_session_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [order_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [order_item_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [tournament_registration_id] IS NULL THEN 0 ELSE 1 END) = 1),
        CONSTRAINT [CK_wallet_transactions_type_source] CHECK (([transaction_type] = 'TopUpCredit' AND [top_up_id] IS NOT NULL AND [amount] > 0) OR ([transaction_type] = 'GamingCharge' AND [gaming_session_id] IS NOT NULL AND [amount] < 0) OR ([transaction_type] = 'OrderDebit' AND [order_id] IS NOT NULL AND [amount] < 0) OR ([transaction_type] = 'OrderItemRefund' AND [order_item_id] IS NOT NULL AND [amount] > 0) OR ([transaction_type] = 'TournamentFee' AND [tournament_registration_id] IS NOT NULL AND [amount] < 0) OR ([transaction_type] = 'TournamentRefund' AND [tournament_registration_id] IS NOT NULL AND [amount] > 0)),
        CONSTRAINT [FK_wallet_transactions_accounts_recorded_by_account_id] FOREIGN KEY ([recorded_by_account_id]) REFERENCES [accounts] ([id]),
        CONSTRAINT [FK_wallet_transactions_customers_customer_id] FOREIGN KEY ([customer_id]) REFERENCES [customers] ([id]),
        CONSTRAINT [FK_wallet_transactions_gaming_sessions_gaming_session_id] FOREIGN KEY ([gaming_session_id]) REFERENCES [gaming_sessions] ([id]),
        CONSTRAINT [FK_wallet_transactions_order_items_order_item_id] FOREIGN KEY ([order_item_id]) REFERENCES [order_items] ([id]),
        CONSTRAINT [FK_wallet_transactions_orders_order_id] FOREIGN KEY ([order_id]) REFERENCES [orders] ([id]),
        CONSTRAINT [FK_wallet_transactions_top_ups_top_up_id] FOREIGN KEY ([top_up_id]) REFERENCES [top_ups] ([id]),
        CONSTRAINT [FK_wallet_transactions_tournament_registrations_tournament_registration_id] FOREIGN KEY ([tournament_registration_id]) REFERENCES [tournament_registrations] ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'id', N'hourly_rate', N'name') AND [object_id] = OBJECT_ID(N'[computer_types]'))
        SET IDENTITY_INSERT [computer_types] ON;
    EXEC(N'INSERT INTO [computer_types] ([id], [hourly_rate], [name])
    VALUES (CAST(1 AS bigint), 8000.0, ''Normal''),
    (CAST(2 AS bigint), 12000.0, ''FPS''),
    (CAST(3 AS bigint), 15000.0, ''Couple''),
    (CAST(4 AS bigint), 16000.0, ''FPS+''),
    (CAST(5 AS bigint), 22000.0, ''Stream'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'id', N'hourly_rate', N'name') AND [object_id] = OBJECT_ID(N'[computer_types]'))
        SET IDENTITY_INSERT [computer_types] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_accounts_created_by_account_id] ON [accounts] ([created_by_account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_accounts_phone] ON [accounts] ([phone]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_computer_types_name] ON [computer_types] ([name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_computers_code] ON [computers] ([code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_computers_computer_type_id] ON [computers] ([computer_type_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_customers_account_id] ON [customers] ([account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_gaming_sessions_computer_id_status] ON [gaming_sessions] ([computer_id], [status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_gaming_sessions_customer_id_status] ON [gaming_sessions] ([customer_id], [status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_gaming_sessions_ended_by_account_id] ON [gaming_sessions] ([ended_by_account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_gaming_sessions_active_computer] ON [gaming_sessions] ([computer_id]) WHERE [status] = ''Active''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_gaming_sessions_active_customer] ON [gaming_sessions] ([customer_id]) WHERE [status] = ''Active''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_order_item_cancel_requests_handled_by_account_id] ON [order_item_cancel_requests] ([handled_by_account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_order_item_cancel_requests_requested_by_customer_id] ON [order_item_cancel_requests] ([requested_by_customer_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_cancel_requests_pending_item] ON [order_item_cancel_requests] ([order_item_id]) WHERE [status] = ''Pending''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_order_items_cancelled_by_account_id] ON [order_items] ([cancelled_by_account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_order_items_confirmed_by_account_id] ON [order_items] ([confirmed_by_account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_order_items_order_id] ON [order_items] ([order_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_order_items_product_id] ON [order_items] ([product_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_orders_customer_id] ON [orders] ([customer_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_orders_gaming_session_id] ON [orders] ([gaming_session_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_player_stats_snapshots_tournament_registration_id] ON [player_stats_snapshots] ([tournament_registration_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_product_categories_name] ON [product_categories] ([name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_products_category_id] ON [products] ([category_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_team_generation_runs_generated_by_account_id] ON [team_generation_runs] ([generated_by_account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_team_generation_runs_tournament_id] ON [team_generation_runs] ([tournament_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_team_members_team_id] ON [team_members] ([team_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_team_members_tournament_registration_id] ON [team_members] ([tournament_registration_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_team_proposals_generation_run_id_tournament_registration_id] ON [team_proposals] ([generation_run_id], [tournament_registration_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_team_proposals_tournament_registration_id] ON [team_proposals] ([tournament_registration_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_teams_finalized_by_account_id] ON [teams] ([finalized_by_account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_teams_tournament_id_team_number] ON [teams] ([tournament_id], [team_number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_top_ups_collected_by_account_id] ON [top_ups] ([collected_by_account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_top_ups_credited_by_account_id] ON [top_ups] ([credited_by_account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_top_ups_one_uncollected_per_customer] ON [top_ups] ([customer_id]) WHERE [status] = ''CreditedUnpaid''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_tournament_registrations_customer_id] ON [tournament_registrations] ([customer_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_tournament_registrations_tournament_id_customer_id] ON [tournament_registrations] ([tournament_id], [customer_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_tournament_registrations_tournament_id_registered_at] ON [tournament_registrations] ([tournament_id], [registered_at]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_tournaments_cancelled_by_account_id] ON [tournaments] ([cancelled_by_account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_tournaments_created_by_account_id] ON [tournaments] ([created_by_account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_wallet_transactions_customer_id_recorded_at] ON [wallet_transactions] ([customer_id], [recorded_at]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_wallet_transactions_gaming_session_id] ON [wallet_transactions] ([gaming_session_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_wallet_transactions_order_id] ON [wallet_transactions] ([order_id]) WHERE [order_id] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_wallet_transactions_order_item_id] ON [wallet_transactions] ([order_item_id]) WHERE [order_item_id] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_wallet_transactions_recorded_by_account_id] ON [wallet_transactions] ([recorded_by_account_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_wallet_transactions_top_up_id] ON [wallet_transactions] ([top_up_id]) WHERE [top_up_id] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_wallet_transactions_registration_fee] ON [wallet_transactions] ([tournament_registration_id]) WHERE [tournament_registration_id] IS NOT NULL AND [transaction_type] = ''TournamentFee''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_wallet_transactions_registration_refund] ON [wallet_transactions] ([tournament_registration_id]) WHERE [tournament_registration_id] IS NOT NULL AND [transaction_type] = ''TournamentRefund''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020192706_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020192706_InitialCreate', N'10.0.13');
END;

COMMIT;
GO

