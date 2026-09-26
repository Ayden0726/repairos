namespace WorkshopOS.Domain.Security;

public static class PermissionKeys
{
    public static readonly (string Key, string Group, string Label)[] Catalogue =
    [
        ("tickets.view", "Tickets", "View tickets"),
        ("tickets.create", "Tickets", "Create tickets"),
        ("tickets.edit", "Tickets", "Edit tickets"),
        ("tickets.delete", "Tickets", "Delete or archive tickets"),
        ("tickets.assign", "Tickets", "Assign technicians"),
        ("tickets.status", "Tickets", "Change ticket status"),
        ("tickets.internal_notes", "Tickets", "View and write internal notes"),
        ("tickets.credentials.view", "Tickets", "Reveal device passwords"),
        ("customers.view", "Customers", "View customers"),
        ("customers.manage", "Customers", "Manage customers"),
        ("devices.view", "Devices", "View devices"),
        ("devices.manage", "Devices", "Manage devices"),
        ("inventory.view", "Inventory", "View inventory"),
        ("inventory.manage", "Inventory", "Manage inventory"),
        ("inventory.purchase_orders", "Inventory", "Purchase orders"),
        ("quotes.view", "Finance", "View quotes"),
        ("quotes.manage", "Finance", "Manage quotes"),
        ("invoices.view", "Finance", "View invoices"),
        ("invoices.manage", "Finance", "Manage invoices"),
        ("payments.view", "Finance", "View payments"),
        ("payments.record", "Finance", "Record payments"),
        ("payments.refund", "Finance", "Issue refunds"),
        ("pricing.view", "Finance", "View pricing"),
        ("pricing.edit", "Finance", "Edit pricing"),
        ("pricing.view_cost", "Finance", "View part cost"),
        ("pricing.view_profit", "Finance", "View profit and margin"),
        ("pricing.change_markup", "Finance", "Change markup"),
        ("pricing.override_labour", "Finance", "Override labour fee"),
        ("pricing.apply_discount", "Finance", "Apply discounts"),
        ("pricing.override_price", "Finance", "Override recommended price"),
        ("pricing.approve_low_margin", "Finance", "Approve below-minimum margin quotes"),
        ("pricing.edit_settings", "Finance", "Edit pricing settings"),
        ("reports.view", "Reports", "View reports"),
        ("reports.financial", "Reports", "View financial reports"),
        ("builds.view", "PC Builds", "View PC builds"),
        ("builds.manage", "PC Builds", "Manage PC builds"),
        ("used.view", "Used Tech", "View used tech"),
        ("used.manage", "Used Tech", "Manage used tech"),
        ("bookings.view", "Calendar", "View bookings"),
        ("bookings.manage", "Calendar", "Manage bookings"),
        ("staff.view", "Staff", "View staff"),
        ("staff.manage", "Staff", "Manage staff"),
        ("roles.manage", "Staff", "Manage roles"),
        ("knowledge.view", "Knowledge", "View knowledge"),
        ("knowledge.manage", "Knowledge", "Manage knowledge"),
        ("ai.use", "AI", "Use AI assistant"),
        ("settings.view", "Administration", "View settings"),
        ("settings.manage", "Administration", "Manage settings"),
        ("integrations.manage", "Administration", "Manage integrations"),
        ("backups.manage", "Administration", "Manage backups"),
        ("audit.view", "Administration", "View audit log"),
        ("incidents.manage", "Administration", "Manage incidents"),
        ("privacy.manage", "Administration", "Manage privacy")
    ];

    public static IReadOnlyList<string> AllKeys { get; } = Catalogue.Select(c => c.Key).ToArray();

    public static IReadOnlyDictionary<string, string[]> DefaultRolePermissions { get; } = new Dictionary<string, string[]>
    {
        ["owner"] = AllKeys.ToArray(),
        ["administrator"] = AllKeys.ToArray(),
        ["manager"] = AllKeys.Where(k => k is not ("roles.manage" or "incidents.manage" or "privacy.manage")).ToArray(),
        ["technician"] =
        [
            "tickets.view", "tickets.create", "tickets.edit", "tickets.assign", "tickets.status",
            "tickets.internal_notes", "tickets.credentials.view",
            "customers.view", "devices.view", "inventory.view", "quotes.view", "quotes.manage",
            "pricing.view", "pricing.apply_discount",
            "builds.view", "builds.manage", "used.view", "bookings.view",
            "knowledge.view", "knowledge.manage", "ai.use"
        ],
        ["front_desk"] =
        [
            "tickets.view", "tickets.create", "tickets.edit", "tickets.assign", "tickets.status",
            "customers.view", "customers.manage", "devices.view", "devices.manage", "inventory.view",
            "quotes.view", "quotes.manage", "invoices.view", "invoices.manage",
            "payments.view", "payments.record", "pricing.view", "pricing.apply_discount",
            "builds.view", "used.view",
            "bookings.view", "bookings.manage", "knowledge.view", "ai.use"
        ],
        ["sales"] =
        [
            "customers.view", "customers.manage", "quotes.view", "quotes.manage",
            "pricing.view", "pricing.view_cost", "pricing.view_profit", "pricing.change_markup",
            "pricing.apply_discount", "pricing.override_labour",
            "used.view", "used.manage", "inventory.view", "builds.view", "ai.use"
        ],
        ["read_only"] =
        [
            "tickets.view", "customers.view", "devices.view", "inventory.view",
            "quotes.view", "invoices.view", "payments.view", "builds.view",
            "used.view", "bookings.view", "knowledge.view", "reports.view", "pricing.view"
        ]
    };
}
