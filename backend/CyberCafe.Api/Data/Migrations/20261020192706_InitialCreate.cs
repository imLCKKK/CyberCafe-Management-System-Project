using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CyberCafe.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    phone = table.Column<string>(type: "varchar(20)", nullable: false),
                    display_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "varchar(255)", nullable: false),
                    role = table.Column<string>(type: "varchar(30)", nullable: false),
                    must_change_password = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    created_by_account_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounts_accounts_created_by_account_id",
                        column: x => x.created_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "computer_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "varchar(30)", nullable: false),
                    hourly_rate = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_computer_types", x => x.id);
                    table.CheckConstraint("CK_computer_types_rate", "[hourly_rate] > 0");
                });

            migrationBuilder.CreateTable(
                name: "product_categories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    wallet_balance = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false, defaultValue: 0m),
                    wallet_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                    table.CheckConstraint("CK_customers_wallet_balance", "[wallet_balance] >= 0");
                    table.ForeignKey(
                        name: "FK_customers_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tournaments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    registration_closes_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    status = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "Open"),
                    created_by_account_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    teams_finalized_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    cancelled_by_account_id = table.Column<long>(type: "bigint", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournaments", x => x.id);
                    table.ForeignKey(
                        name: "FK_tournaments_accounts_cancelled_by_account_id",
                        column: x => x.cancelled_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_tournaments_accounts_created_by_account_id",
                        column: x => x.created_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "computers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "varchar(30)", nullable: false),
                    computer_type_id = table.Column<long>(type: "bigint", nullable: false),
                    is_under_maintenance = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_computers", x => x.id);
                    table.ForeignKey(
                        name: "FK_computers_computer_types_computer_type_id",
                        column: x => x.computer_type_id,
                        principalTable: "computer_types",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    category_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    current_price = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.CheckConstraint("CK_products_price", "[current_price] >= 0");
                    table.ForeignKey(
                        name: "FK_products_product_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "product_categories",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "top_ups",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    requested_by_customer = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    status = table.Column<string>(type: "varchar(30)", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    credited_by_account_id = table.Column<long>(type: "bigint", nullable: true),
                    credited_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    collected_by_account_id = table.Column<long>(type: "bigint", nullable: true),
                    collected_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    collection_method = table.Column<string>(type: "varchar(30)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_top_ups", x => x.id);
                    table.CheckConstraint("CK_top_ups_amount", "[amount] > 0");
                    table.ForeignKey(
                        name: "FK_top_ups_accounts_collected_by_account_id",
                        column: x => x.collected_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_top_ups_accounts_credited_by_account_id",
                        column: x => x.credited_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_top_ups_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "team_generation_runs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tournament_id = table.Column<long>(type: "bigint", nullable: false),
                    model_name = table.Column<string>(type: "varchar(100)", nullable: false),
                    model_version = table.Column<string>(type: "varchar(50)", nullable: false),
                    generated_by_account_id = table.Column<long>(type: "bigint", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_generation_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_generation_runs_accounts_generated_by_account_id",
                        column: x => x.generated_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_team_generation_runs_tournaments_tournament_id",
                        column: x => x.tournament_id,
                        principalTable: "tournaments",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tournament_id = table.Column<long>(type: "bigint", nullable: false),
                    team_number = table.Column<int>(type: "int", nullable: false),
                    finalized_by_account_id = table.Column<long>(type: "bigint", nullable: false),
                    finalized_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teams", x => x.id);
                    table.CheckConstraint("CK_teams_number", "[team_number] > 0");
                    table.ForeignKey(
                        name: "FK_teams_accounts_finalized_by_account_id",
                        column: x => x.finalized_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_teams_tournaments_tournament_id",
                        column: x => x.tournament_id,
                        principalTable: "tournaments",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tournament_registrations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tournament_id = table.Column<long>(type: "bigint", nullable: false),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    riot_game_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    riot_tagline = table.Column<string>(type: "varchar(20)", nullable: false),
                    fee_amount_snapshot = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false, defaultValue: 50000m),
                    fee_status = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "Paid"),
                    registered_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    refunded_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    refund_reason = table.Column<string>(type: "varchar(30)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_registrations", x => x.id);
                    table.CheckConstraint("CK_tournament_registrations_fee", "[fee_amount_snapshot] > 0");
                    table.ForeignKey(
                        name: "FK_tournament_registrations_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_tournament_registrations_tournaments_tournament_id",
                        column: x => x.tournament_id,
                        principalTable: "tournaments",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "gaming_sessions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    computer_id = table.Column<long>(type: "bigint", nullable: false),
                    hourly_rate_snapshot = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    last_charged_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    charged_amount = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false, defaultValue: 0m),
                    status = table.Column<string>(type: "varchar(30)", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    end_reason = table.Column<string>(type: "varchar(30)", nullable: true),
                    ended_by_account_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gaming_sessions", x => x.id);
                    table.CheckConstraint("CK_gaming_sessions_amounts", "[hourly_rate_snapshot] > 0 AND [charged_amount] >= 0");
                    table.CheckConstraint("CK_gaming_sessions_time", "[last_charged_at] >= [started_at] AND ([ended_at] IS NULL OR [ended_at] >= [started_at])");
                    table.ForeignKey(
                        name: "FK_gaming_sessions_accounts_ended_by_account_id",
                        column: x => x.ended_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_gaming_sessions_computers_computer_id",
                        column: x => x.computer_id,
                        principalTable: "computers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_gaming_sessions_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "player_stats_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tournament_registration_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "varchar(30)", nullable: false),
                    source = table.Column<string>(type: "varchar(30)", nullable: true),
                    scope = table.Column<string>(type: "varchar(80)", nullable: true),
                    matches = table.Column<int>(type: "int", nullable: true),
                    win_rate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    headshot_rate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    damage_per_round = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    kda = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    source_updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_stats_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_player_stats_snapshots_tournament_registrations_tournament_registration_id",
                        column: x => x.tournament_registration_id,
                        principalTable: "tournament_registrations",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "team_members",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    team_id = table.Column<long>(type: "bigint", nullable: false),
                    tournament_registration_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_members_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_team_members_tournament_registrations_tournament_registration_id",
                        column: x => x.tournament_registration_id,
                        principalTable: "tournament_registrations",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "team_proposals",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    generation_run_id = table.Column<long>(type: "bigint", nullable: false),
                    tournament_registration_id = table.Column<long>(type: "bigint", nullable: false),
                    proposed_team_number = table.Column<int>(type: "int", nullable: false),
                    predicted_skill_score = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_proposals", x => x.id);
                    table.CheckConstraint("CK_team_proposals_number", "[proposed_team_number] > 0");
                    table.ForeignKey(
                        name: "FK_team_proposals_team_generation_runs_generation_run_id",
                        column: x => x.generation_run_id,
                        principalTable: "team_generation_runs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_team_proposals_tournament_registrations_tournament_registration_id",
                        column: x => x.tournament_registration_id,
                        principalTable: "tournament_registrations",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    gaming_session_id = table.Column<long>(type: "bigint", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    placed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                    table.CheckConstraint("CK_orders_total", "[total_amount] >= 0");
                    table.ForeignKey(
                        name: "FK_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_orders_gaming_sessions_gaming_session_id",
                        column: x => x.gaming_session_id,
                        principalTable: "gaming_sessions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    product_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    unit_price_snapshot = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    status = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "Pending"),
                    confirmed_by_account_id = table.Column<long>(type: "bigint", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    cancelled_by_account_id = table.Column<long>(type: "bigint", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.id);
                    table.CheckConstraint("CK_order_items_values", "[quantity] > 0 AND [unit_price_snapshot] >= 0");
                    table.ForeignKey(
                        name: "FK_order_items_accounts_cancelled_by_account_id",
                        column: x => x.cancelled_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_order_items_accounts_confirmed_by_account_id",
                        column: x => x.confirmed_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_order_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "order_item_cancel_requests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    order_item_id = table.Column<long>(type: "bigint", nullable: false),
                    requested_by_customer_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "varchar(30)", nullable: false, defaultValue: "Pending"),
                    requested_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    handled_by_account_id = table.Column<long>(type: "bigint", nullable: true),
                    handled_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_item_cancel_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_item_cancel_requests_accounts_handled_by_account_id",
                        column: x => x.handled_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_order_item_cancel_requests_customers_requested_by_customer_id",
                        column: x => x.requested_by_customer_id,
                        principalTable: "customers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_order_item_cancel_requests_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "wallet_transactions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<long>(type: "bigint", nullable: false),
                    transaction_type = table.Column<string>(type: "varchar(30)", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    balance_after = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    top_up_id = table.Column<long>(type: "bigint", nullable: true),
                    gaming_session_id = table.Column<long>(type: "bigint", nullable: true),
                    order_id = table.Column<long>(type: "bigint", nullable: true),
                    order_item_id = table.Column<long>(type: "bigint", nullable: true),
                    tournament_registration_id = table.Column<long>(type: "bigint", nullable: true),
                    recorded_by_account_id = table.Column<long>(type: "bigint", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallet_transactions", x => x.id);
                    table.CheckConstraint("CK_wallet_transactions_balance", "[balance_after] >= 0");
                    table.CheckConstraint("CK_wallet_transactions_source", "(CASE WHEN [top_up_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [gaming_session_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [order_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [order_item_id] IS NULL THEN 0 ELSE 1 END + CASE WHEN [tournament_registration_id] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.CheckConstraint("CK_wallet_transactions_type_source", "([transaction_type] = 'TopUpCredit' AND [top_up_id] IS NOT NULL AND [amount] > 0) OR ([transaction_type] = 'GamingCharge' AND [gaming_session_id] IS NOT NULL AND [amount] < 0) OR ([transaction_type] = 'OrderDebit' AND [order_id] IS NOT NULL AND [amount] < 0) OR ([transaction_type] = 'OrderItemRefund' AND [order_item_id] IS NOT NULL AND [amount] > 0) OR ([transaction_type] = 'TournamentFee' AND [tournament_registration_id] IS NOT NULL AND [amount] < 0) OR ([transaction_type] = 'TournamentRefund' AND [tournament_registration_id] IS NOT NULL AND [amount] > 0)");
                    table.ForeignKey(
                        name: "FK_wallet_transactions_accounts_recorded_by_account_id",
                        column: x => x.recorded_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_wallet_transactions_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_wallet_transactions_gaming_sessions_gaming_session_id",
                        column: x => x.gaming_session_id,
                        principalTable: "gaming_sessions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_wallet_transactions_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_wallet_transactions_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_wallet_transactions_top_ups_top_up_id",
                        column: x => x.top_up_id,
                        principalTable: "top_ups",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_wallet_transactions_tournament_registrations_tournament_registration_id",
                        column: x => x.tournament_registration_id,
                        principalTable: "tournament_registrations",
                        principalColumn: "id");
                });

            migrationBuilder.InsertData(
                table: "computer_types",
                columns: new[] { "id", "hourly_rate", "name" },
                values: new object[,]
                {
                    { 1L, 8000m, "Normal" },
                    { 2L, 12000m, "FPS" },
                    { 3L, 15000m, "Couple" },
                    { 4L, 16000m, "FPS+" },
                    { 5L, 22000m, "Stream" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_created_by_account_id",
                table: "accounts",
                column: "created_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_phone",
                table: "accounts",
                column: "phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_computer_types_name",
                table: "computer_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_computers_code",
                table: "computers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_computers_computer_type_id",
                table: "computers",
                column: "computer_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_customers_account_id",
                table: "customers",
                column: "account_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gaming_sessions_computer_id_status",
                table: "gaming_sessions",
                columns: new[] { "computer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_gaming_sessions_customer_id_status",
                table: "gaming_sessions",
                columns: new[] { "customer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_gaming_sessions_ended_by_account_id",
                table: "gaming_sessions",
                column: "ended_by_account_id");

            migrationBuilder.CreateIndex(
                name: "UX_gaming_sessions_active_computer",
                table: "gaming_sessions",
                column: "computer_id",
                unique: true,
                filter: "[status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "UX_gaming_sessions_active_customer",
                table: "gaming_sessions",
                column: "customer_id",
                unique: true,
                filter: "[status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_order_item_cancel_requests_handled_by_account_id",
                table: "order_item_cancel_requests",
                column: "handled_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_item_cancel_requests_requested_by_customer_id",
                table: "order_item_cancel_requests",
                column: "requested_by_customer_id");

            migrationBuilder.CreateIndex(
                name: "UX_cancel_requests_pending_item",
                table: "order_item_cancel_requests",
                column: "order_item_id",
                unique: true,
                filter: "[status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_cancelled_by_account_id",
                table: "order_items",
                column: "cancelled_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_confirmed_by_account_id",
                table: "order_items",
                column: "confirmed_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_product_id",
                table: "order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_customer_id",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_gaming_session_id",
                table: "orders",
                column: "gaming_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_player_stats_snapshots_tournament_registration_id",
                table: "player_stats_snapshots",
                column: "tournament_registration_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_categories_name",
                table: "product_categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_generation_runs_generated_by_account_id",
                table: "team_generation_runs",
                column: "generated_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_generation_runs_tournament_id",
                table: "team_generation_runs",
                column: "tournament_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_members_team_id",
                table: "team_members",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_members_tournament_registration_id",
                table: "team_members",
                column: "tournament_registration_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_proposals_generation_run_id_tournament_registration_id",
                table: "team_proposals",
                columns: new[] { "generation_run_id", "tournament_registration_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_proposals_tournament_registration_id",
                table: "team_proposals",
                column: "tournament_registration_id");

            migrationBuilder.CreateIndex(
                name: "IX_teams_finalized_by_account_id",
                table: "teams",
                column: "finalized_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_teams_tournament_id_team_number",
                table: "teams",
                columns: new[] { "tournament_id", "team_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_top_ups_collected_by_account_id",
                table: "top_ups",
                column: "collected_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_top_ups_credited_by_account_id",
                table: "top_ups",
                column: "credited_by_account_id");

            migrationBuilder.CreateIndex(
                name: "UX_top_ups_one_uncollected_per_customer",
                table: "top_ups",
                column: "customer_id",
                unique: true,
                filter: "[status] = 'CreditedUnpaid'");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_registrations_customer_id",
                table: "tournament_registrations",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_registrations_tournament_id_customer_id",
                table: "tournament_registrations",
                columns: new[] { "tournament_id", "customer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournament_registrations_tournament_id_registered_at",
                table: "tournament_registrations",
                columns: new[] { "tournament_id", "registered_at" });

            migrationBuilder.CreateIndex(
                name: "IX_tournaments_cancelled_by_account_id",
                table: "tournaments",
                column: "cancelled_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_tournaments_created_by_account_id",
                table: "tournaments",
                column: "created_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_customer_id_recorded_at",
                table: "wallet_transactions",
                columns: new[] { "customer_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_gaming_session_id",
                table: "wallet_transactions",
                column: "gaming_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_order_id",
                table: "wallet_transactions",
                column: "order_id",
                unique: true,
                filter: "[order_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_order_item_id",
                table: "wallet_transactions",
                column: "order_item_id",
                unique: true,
                filter: "[order_item_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_recorded_by_account_id",
                table: "wallet_transactions",
                column: "recorded_by_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_top_up_id",
                table: "wallet_transactions",
                column: "top_up_id",
                unique: true,
                filter: "[top_up_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_wallet_transactions_registration_fee",
                table: "wallet_transactions",
                column: "tournament_registration_id",
                unique: true,
                filter: "[tournament_registration_id] IS NOT NULL AND [transaction_type] = 'TournamentFee'");

            migrationBuilder.CreateIndex(
                name: "UX_wallet_transactions_registration_refund",
                table: "wallet_transactions",
                column: "tournament_registration_id",
                unique: true,
                filter: "[tournament_registration_id] IS NOT NULL AND [transaction_type] = 'TournamentRefund'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_item_cancel_requests");

            migrationBuilder.DropTable(
                name: "player_stats_snapshots");

            migrationBuilder.DropTable(
                name: "team_members");

            migrationBuilder.DropTable(
                name: "team_proposals");

            migrationBuilder.DropTable(
                name: "wallet_transactions");

            migrationBuilder.DropTable(
                name: "teams");

            migrationBuilder.DropTable(
                name: "team_generation_runs");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "top_ups");

            migrationBuilder.DropTable(
                name: "tournament_registrations");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "tournaments");

            migrationBuilder.DropTable(
                name: "gaming_sessions");

            migrationBuilder.DropTable(
                name: "product_categories");

            migrationBuilder.DropTable(
                name: "computers");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "computer_types");

            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
