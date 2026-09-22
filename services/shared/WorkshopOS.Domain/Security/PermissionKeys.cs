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
            "tickets.view", "tickets.create", "tickets.edit", "tickets.assign",
            "customers.view", "devices.view", "inventory.view", "quotes.view",
            "builds.view", "builds.manage", "used.view", "bookings.view",
            "knowledge.view", "knowledge.manage", "ai.use"
        ],
        ["front_desk"] =
        [
            "tickets.view", "tickets.create", "tickets.edit", "tickets.assign",
            "customers.view", "customers.manage", "devices.view", "inventory.view",
            "quotes.view", "quotes.manage", "invoices.view", "invoices.manage",
            "payments.view", "payments.record", "builds.view", "used.view",
            "bookings.view", "bookings.manage", "knowledge.view", "ai.use"
        ],
        ["sales"] =
        [
            "customers.view", "customers.manage", "quotes.view", "quotes.manage",
            "used.view", "used.manage", "inventory.view", "builds.view", "ai.use"
        ],
        ["read_only"] =
        [
            "tickets.view", "customers.view", "devices.view", "inventory.view",
            "quotes.view", "invoices.view", "payments.view", "builds.view",
            "used.view", "bookings.view", "knowledge.view", "reports.view"
        ]
    };
}
