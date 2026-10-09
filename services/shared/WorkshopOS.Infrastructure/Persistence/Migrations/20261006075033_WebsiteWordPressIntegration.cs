using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WebsiteWordPressIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessPinHash",
                table: "repair_tickets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EstimatedCompletionAt",
                table: "repair_tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicAccessToken",
                table: "repair_tickets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "custom_pc_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Budget = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Usage = table.Column<string>(type: "text", nullable: true),
                    PreferredGamesSoftware = table.Column<string>(type: "text", nullable: true),
                    MonitorResolution = table.Column<string>(type: "text", nullable: true),
                    PerformanceTarget = table.Column<string>(type: "text", nullable: true),
                    NeedsWifiBluetooth = table.Column<bool>(type: "boolean", nullable: true),
                    RgbPreferences = table.Column<string>(type: "text", nullable: true),
                    CaseSize = table.Column<string>(type: "text", nullable: true),
                    Colour = table.Column<string>(type: "text", nullable: true),
                    StorageRequirements = table.Column<string>(type: "text", nullable: true),
                    Upgradeability = table.Column<string>(type: "text", nullable: true),
                    ContactName = table.Column<string>(type: "text", nullable: false),
                    ContactPhone = table.Column<string>(type: "text", nullable: true),
                    ContactEmail = table.Column<string>(type: "text", nullable: true),
                    CustomerNotes = table.Column<string>(type: "text", nullable: true),
                    PcBuildId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_pc_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_custom_pc_requests_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_custom_pc_requests_pc_builds_PcBuildId",
                        column: x => x.PcBuildId,
                        principalTable: "pc_builds",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "customer_portal_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MustResetPassword = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_portal_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_portal_accounts_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ToAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    RepairTicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Subject = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "quote_approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepairTicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteRevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    AccessMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quote_approvals_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refurb_diagnostic_checklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refurb_diagnostic_checklists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "refurb_diagnostic_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsedDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultsJson = table.Column<string>(type: "text", nullable: false),
                    OverallGrade = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CompletedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refurb_diagnostic_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refurb_diagnostic_results_used_devices_UsedDeviceId",
                        column: x => x.UsedDeviceId,
                        principalTable: "used_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sell_device_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Storage = table.Column<string>(type: "text", nullable: true),
                    Ram = table.Column<string>(type: "text", nullable: true),
                    Cpu = table.Column<string>(type: "text", nullable: true),
                    Gpu = table.Column<string>(type: "text", nullable: true),
                    Colour = table.Column<string>(type: "text", nullable: true),
                    SerialOrImei = table.Column<string>(type: "text", nullable: true),
                    BatteryHealth = table.Column<string>(type: "text", nullable: true),
                    CarrierStatus = table.Column<string>(type: "text", nullable: true),
                    PowersOn = table.Column<bool>(type: "boolean", nullable: true),
                    Charges = table.Column<bool>(type: "boolean", nullable: true),
                    ScreenCondition = table.Column<string>(type: "text", nullable: true),
                    BodyCondition = table.Column<string>(type: "text", nullable: true),
                    CameraOk = table.Column<bool>(type: "boolean", nullable: true),
                    SpeakerMicOk = table.Column<bool>(type: "boolean", nullable: true),
                    ButtonsOk = table.Column<bool>(type: "boolean", nullable: true),
                    BiometricsOk = table.Column<bool>(type: "boolean", nullable: true),
                    LiquidDamage = table.Column<bool>(type: "boolean", nullable: true),
                    KnownFaults = table.Column<string>(type: "text", nullable: true),
                    AccountLocked = table.Column<bool>(type: "boolean", nullable: true),
                    FindMyEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    GoogleSamsungRemoved = table.Column<bool>(type: "boolean", nullable: true),
                    CustomerNotes = table.Column<string>(type: "text", nullable: true),
                    ContactName = table.Column<string>(type: "text", nullable: false),
                    ContactPhone = table.Column<string>(type: "text", nullable: true),
                    ContactEmail = table.Column<string>(type: "text", nullable: true),
                    Suburb = table.Column<string>(type: "text", nullable: true),
                    PreferredContact = table.Column<string>(type: "text", nullable: false),
                    PhotoUrlsJson = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true),
                    InternalNotes = table.Column<string>(type: "text", nullable: true),
                    UsedDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sell_device_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sell_device_requests_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sell_device_requests_used_devices_UsedDeviceId",
                        column: x => x.UsedDeviceId,
                        principalTable: "used_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "warranties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepairTicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsedDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CoverageSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    WarrantyDays = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warranties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warranties_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_warranties_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_warranties_repair_tickets_RepairTicketId",
                        column: x => x.RepairTicketId,
                        principalTable: "repair_tickets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_warranties_used_devices_UsedDeviceId",
                        column: x => x.UsedDeviceId,
                        principalTable: "used_devices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "website_api_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AllowedOrigins = table.Column<string>(type: "text", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_website_api_keys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "conversation_threads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    RepairTicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    SellDeviceRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_threads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conversation_threads_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_conversation_threads_repair_tickets_RepairTicketId",
                        column: x => x.RepairTicketId,
                        principalTable: "repair_tickets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_conversation_threads_sell_device_requests_SellDeviceRequest~",
                        column: x => x.SellDeviceRequestId,
                        principalTable: "sell_device_requests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "sell_device_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sell_device_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sell_device_events_sell_device_requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "sell_device_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sell_device_offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimatedResalePrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PurchaseCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EstimatedPartsCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    LabourAllowance = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    SellingPlatformFees = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ShippingPacking = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    RiskAllowance = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DesiredMinProfit = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DesiredMinMarginPercent = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    RecommendedOfferMin = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    RecommendedOffer = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    RecommendedOfferMax = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    AbsoluteMaxPurchasePrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EstimatedTotalInvestment = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EstimatedResaleProfit = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EstimatedMarginPercent = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CustomerFacingOffer = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sell_device_offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sell_device_offers_sell_device_requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "sell_device_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conversation_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorCustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conversation_messages_conversation_threads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "conversation_threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_repair_tickets_PublicAccessToken",
                table: "repair_tickets",
                column: "PublicAccessToken");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_messages_ThreadId",
                table: "conversation_messages",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_threads_CustomerId",
                table: "conversation_threads",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_threads_RepairTicketId",
                table: "conversation_threads",
                column: "RepairTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_threads_SellDeviceRequestId",
                table: "conversation_threads",
                column: "SellDeviceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_custom_pc_requests_CustomerId",
                table: "custom_pc_requests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_custom_pc_requests_PcBuildId",
                table: "custom_pc_requests",
                column: "PcBuildId");

            migrationBuilder.CreateIndex(
                name: "IX_custom_pc_requests_Reference",
                table: "custom_pc_requests",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_portal_accounts_CustomerId",
                table: "customer_portal_accounts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_portal_accounts_NormalizedEmail",
                table: "customer_portal_accounts",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_Status",
                table: "notification_outbox",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_notification_templates_Key",
                table: "notification_templates",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quote_approvals_QuoteId",
                table: "quote_approvals",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_refurb_diagnostic_results_UsedDeviceId",
                table: "refurb_diagnostic_results",
                column: "UsedDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_sell_device_events_RequestId",
                table: "sell_device_events",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_sell_device_offers_RequestId",
                table: "sell_device_offers",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sell_device_requests_CustomerId",
                table: "sell_device_requests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_sell_device_requests_Reference",
                table: "sell_device_requests",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sell_device_requests_Status",
                table: "sell_device_requests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_sell_device_requests_UsedDeviceId",
                table: "sell_device_requests",
                column: "UsedDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_warranties_CustomerId",
                table: "warranties",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_warranties_DeviceId",
                table: "warranties",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_warranties_ExpiresAt",
                table: "warranties",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_warranties_RepairTicketId",
                table: "warranties",
                column: "RepairTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_warranties_UsedDeviceId",
                table: "warranties",
                column: "UsedDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_website_api_keys_KeyPrefix",
                table: "website_api_keys",
                column: "KeyPrefix");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conversation_messages");

            migrationBuilder.DropTable(
                name: "custom_pc_requests");

            migrationBuilder.DropTable(
                name: "customer_portal_accounts");

            migrationBuilder.DropTable(
                name: "notification_outbox");

            migrationBuilder.DropTable(
                name: "notification_templates");

            migrationBuilder.DropTable(
                name: "quote_approvals");

            migrationBuilder.DropTable(
                name: "refurb_diagnostic_checklists");

            migrationBuilder.DropTable(
                name: "refurb_diagnostic_results");

            migrationBuilder.DropTable(
                name: "sell_device_events");

            migrationBuilder.DropTable(
                name: "sell_device_offers");

            migrationBuilder.DropTable(
                name: "warranties");

            migrationBuilder.DropTable(
                name: "website_api_keys");

            migrationBuilder.DropTable(
                name: "conversation_threads");

            migrationBuilder.DropTable(
                name: "sell_device_requests");

            migrationBuilder.DropIndex(
                name: "IX_repair_tickets_PublicAccessToken",
                table: "repair_tickets");

            migrationBuilder.DropColumn(
                name: "AccessPinHash",
                table: "repair_tickets");

            migrationBuilder.DropColumn(
                name: "EstimatedCompletionAt",
                table: "repair_tickets");

            migrationBuilder.DropColumn(
                name: "PublicAccessToken",
                table: "repair_tickets");
        }
    }
}
