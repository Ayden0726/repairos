using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkshopOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2CustomersDevicesRepairs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CompanyName = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    AddressLine1 = table.Column<string>(type: "text", nullable: true),
                    Suburb = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: true),
                    Postcode = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    PreferredContact = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MarketingConsent = table.Column<bool>(type: "boolean", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastVisitAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customers_locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "locations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "document_sequences",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NextValue = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_sequences", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "repair_priorities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repair_priorities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "repair_statuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Colour = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CountsAsWaitingForParts = table.Column<bool>(type: "boolean", nullable: false),
                    CountsAsAwaitingApproval = table.Column<bool>(type: "boolean", nullable: false),
                    CountsAsReadyForPickup = table.Column<bool>(type: "boolean", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repair_statuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "repair_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Prefix = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repair_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Model = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Variant = table.Column<string>(type: "text", nullable: true),
                    Colour = table.Column<string>(type: "text", nullable: true),
                    Serial = table.Column<string>(type: "text", nullable: true),
                    Imei = table.Column<string>(type: "text", nullable: true),
                    StorageCapacity = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_devices_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "repair_tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatusId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportedIssue = table.Column<string>(type: "text", nullable: false),
                    Diagnosis = table.Column<string>(type: "text", nullable: true),
                    RecommendedRepair = table.Column<string>(type: "text", nullable: true),
                    DamageDescription = table.Column<string>(type: "text", nullable: true),
                    HasExistingCracks = table.Column<bool>(type: "boolean", nullable: true),
                    HasScratches = table.Column<bool>(type: "boolean", nullable: true),
                    WaterDamageIndicators = table.Column<bool>(type: "boolean", nullable: true),
                    PowersOn = table.Column<bool>(type: "boolean", nullable: true),
                    AccessoriesIncluded = table.Column<bool>(type: "boolean", nullable: false),
                    ChargerIncluded = table.Column<bool>(type: "boolean", nullable: false),
                    SimIncluded = table.Column<bool>(type: "boolean", nullable: false),
                    CaseIncluded = table.Column<bool>(type: "boolean", nullable: false),
                    PasscodeHint = table.Column<string>(type: "text", nullable: true),
                    PasscodeEnc = table.Column<string>(type: "text", nullable: true),
                    EstimatedPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    DepositAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CollectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repair_tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_repair_tickets_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_repair_tickets_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_repair_tickets_locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_repair_tickets_repair_priorities_PriorityId",
                        column: x => x.PriorityId,
                        principalTable: "repair_priorities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_repair_tickets_repair_statuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "repair_statuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_repair_tickets_repair_types_TypeId",
                        column: x => x.TypeId,
                        principalTable: "repair_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_repair_tickets_users_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_repair_tickets_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "repair_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repair_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_repair_events_repair_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "repair_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "repair_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repair_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_repair_notes_repair_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "repair_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_repair_notes_users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customers_DisplayName",
                table: "customers",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_customers_Email",
                table: "customers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_customers_LocationId",
                table: "customers",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_customers_Phone",
                table: "customers",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_devices_CustomerId",
                table: "devices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_devices_Imei",
                table: "devices",
                column: "Imei");

            migrationBuilder.CreateIndex(
                name: "IX_devices_Serial",
                table: "devices",
                column: "Serial");

            migrationBuilder.CreateIndex(
                name: "IX_repair_events_TicketId_CreatedAt",
                table: "repair_events",
                columns: new[] { "TicketId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_repair_notes_AuthorId",
                table: "repair_notes",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_notes_TicketId",
                table: "repair_notes",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_priorities_Key",
                table: "repair_priorities",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_repair_statuses_Key",
                table: "repair_statuses",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_repair_tickets_AssignedToId",
                table: "repair_tickets",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_tickets_CreatedAt",
                table: "repair_tickets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_repair_tickets_CreatedById",
                table: "repair_tickets",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_repair_tickets_CustomerId",
                table: "repair_tickets",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_tickets_DeviceId",
                table: "repair_tickets",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_tickets_DueAt",
                table: "repair_tickets",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_repair_tickets_LocationId",
                table: "repair_tickets",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_tickets_PriorityId",
                table: "repair_tickets",
                column: "PriorityId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_tickets_StatusId",
                table: "repair_tickets",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_tickets_TicketNumber",
                table: "repair_tickets",
                column: "TicketNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_repair_tickets_TypeId",
                table: "repair_tickets",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_repair_types_Key",
                table: "repair_types",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_sequences");

            migrationBuilder.DropTable(
                name: "repair_events");

            migrationBuilder.DropTable(
                name: "repair_notes");

            migrationBuilder.DropTable(
                name: "repair_tickets");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "repair_priorities");

            migrationBuilder.DropTable(
                name: "repair_statuses");

            migrationBuilder.DropTable(
                name: "repair_types");

            migrationBuilder.DropTable(
                name: "customers");
        }
    }
}
