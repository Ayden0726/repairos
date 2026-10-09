using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Common;
using WorkshopOS.Contracts.Operations;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Client.Views;

public sealed class CalendarBookingChipVm
{
    public CalendarBookingChipVm(BookingDto booking)
    {
        Booking = booking;
        Id = booking.Id;
        var localStart = booking.StartsAt.ToLocalTime();
        var localEnd = booking.EndsAt.ToLocalTime();
        TimeLabel = $"{localStart:HH:mm}–{localEnd:HH:mm}";
        Title = $"{TimeLabel} · {booking.Type}";
        Subtitle = booking.CustomerName;
        Status = booking.Status;
        IsCancelled = string.Equals(booking.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);
        DetailLine = string.IsNullOrWhiteSpace(booking.Notes)
            ? $"{booking.Type} · {booking.Status}"
            : $"{booking.Type} · {booking.Status} · {booking.Notes}";
    }

    public Guid Id { get; }
    public BookingDto Booking { get; }
    public string TimeLabel { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string Status { get; }
    public string DetailLine { get; }
    public bool IsCancelled { get; }
}

public partial class CalendarDayVm : ObservableObject
{
    public CalendarDayVm(DateTime date, bool isCurrentMonth)
    {
        Date = date.Date;
        IsCurrentMonth = isCurrentMonth;
        IsToday = Date == DateTime.Today;
        DayNumber = date.Day.ToString();
    }

    public DateTime Date { get; }
    public string DayNumber { get; }
    public bool IsCurrentMonth { get; }
    public bool IsToday { get; }
    public ObservableCollection<CalendarBookingChipVm> Bookings { get; } = new();

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _summary = string.Empty;

    public void SetBookings(IEnumerable<BookingDto> bookings)
    {
        Bookings.Clear();
        foreach (var b in bookings.OrderBy(x => x.StartsAt))
            Bookings.Add(new CalendarBookingChipVm(b));
        var active = Bookings.Count(x => !x.IsCancelled);
        Summary = active == 0
            ? (Bookings.Count == 0 ? string.Empty : $"{Bookings.Count} cancelled")
            : active == 1
                ? Bookings.First(x => !x.IsCancelled).Subtitle
                : $"{active} bookings";
    }
}

public partial class CalendarWeekVm : ObservableObject
{
    public ObservableCollection<CalendarDayVm> Days { get; } = new();
}

public partial class CalendarViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly List<BookingDto> _cache = new();
    private bool _suppressDaySelection;

    [ObservableProperty] private string _monthTitle = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isMonthView = true;
    [ObservableProperty] private bool _isWeekView;
    [ObservableProperty] private string _selectedDayLabel = "Select a day";
    [ObservableProperty] private string _panelTitle = "New booking";
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _showCancelled;
    [ObservableProperty] private ListPickItem? _selectedCustomer;
    [ObservableProperty] private string _bookingType = "Intake";
    [ObservableProperty] private string _bookingNotes = string.Empty;
    [ObservableProperty] private string _bookingStartLocal = string.Empty;
    [ObservableProperty] private string _bookingEndLocal = string.Empty;
    [ObservableProperty] private CalendarBookingChipVm? _selectedBooking;
    [ObservableProperty] private CalendarDayVm? _selectedDay;

    public ObservableCollection<CalendarWeekVm> Weeks { get; } = new();
    public ObservableCollection<CalendarDayVm> Days { get; } = new();
    public ObservableCollection<CalendarBookingChipVm> SelectedDayBookings { get; } = new();
    public ObservableCollection<ListPickItem> Customers { get; } = new();
    public ObservableCollection<string> BookingTypes { get; } = new()
    {
        "Intake", "Diagnostic", "Pickup", "Onsite", "Consult", "Repair", "Other"
    };

    public DateTime VisibleMonth { get; private set; }
    public DateTime VisibleWeekStart { get; private set; }

    public CalendarViewModel(ApiClient api)
    {
        _api = api;
        var today = DateTime.Today;
        VisibleMonth = new DateTime(today.Year, today.Month, 1);
        VisibleWeekStart = StartOfWeek(today);
        PrepareFormForDay(today);
    }

    partial void OnIsMonthViewChanged(bool value)
    {
        if (value) IsWeekView = false;
    }

    partial void OnIsWeekViewChanged(bool value)
    {
        if (value) IsMonthView = false;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Error = null;
        IsBusy = true;
        try
        {
            await LoadCustomersAsync();
            try
            {
                await LoadBookingsForVisibleRangeAsync();
            }
            catch (Exception ex)
            {
                _cache.Clear();
                Error = ex.Message;
            }

            RebuildGrid();
            if (string.IsNullOrWhiteSpace(Error))
            {
                StatusMessage = _cache.Count(b => !string.Equals(b.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)) == 0
                    ? "No bookings in this range. Pick a day and schedule one."
                    : $"{_cache.Count} booking(s) in view";
            }
            else
            {
                StatusMessage = "Calendar grid loaded — bookings request failed (see error).";
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            StatusMessage = null;
            try { RebuildGrid(); } catch { /* keep error */ }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TodayAsync()
    {
        var today = DateTime.Today;
        VisibleMonth = new DateTime(today.Year, today.Month, 1);
        VisibleWeekStart = StartOfWeek(today);
        await RefreshAsync();
        SelectDay(Days.FirstOrDefault(d => d.Date == today) ?? CreateTransientDay(today));
    }

    [RelayCommand]
    private async Task PrevAsync()
    {
        if (IsWeekView)
            VisibleWeekStart = VisibleWeekStart.AddDays(-7);
        else
            VisibleMonth = VisibleMonth.AddMonths(-1);
        VisibleWeekStart = StartOfWeek(IsWeekView ? VisibleWeekStart : VisibleMonth);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        if (IsWeekView)
            VisibleWeekStart = VisibleWeekStart.AddDays(7);
        else
            VisibleMonth = VisibleMonth.AddMonths(1);
        VisibleWeekStart = StartOfWeek(IsWeekView ? VisibleWeekStart : VisibleMonth);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ShowMonthAsync()
    {
        IsMonthView = true;
        IsWeekView = false;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ShowWeekAsync()
    {
        IsWeekView = true;
        IsMonthView = false;
        if (SelectedDay is not null)
            VisibleWeekStart = StartOfWeek(SelectedDay.Date);
        await RefreshAsync();
    }

    public void SelectDay(CalendarDayVm day)
    {
        if (_suppressDaySelection) return;
        foreach (var d in Days)
            d.IsSelected = d.Date == day.Date;
        SelectedDay = day;
        SelectedDayLabel = day.Date.ToString("dddd d MMMM yyyy");
        SelectedBooking = null;
        IsEditing = false;
        PanelTitle = "New booking";
        PrepareFormForDay(day.Date);
        RebuildSelectedDayList();
    }

    public void SelectBooking(CalendarBookingChipVm chip)
    {
        SelectedBooking = chip;
        IsEditing = true;
        PanelTitle = "Edit booking";
        var b = chip.Booking;
        BookingType = b.Type;
        BookingNotes = b.Notes ?? string.Empty;
        BookingStartLocal = b.StartsAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        BookingEndLocal = b.EndsAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        SelectedCustomer = Customers.FirstOrDefault(c => c.Id == b.CustomerId)
            ?? new ListPickItem(b.CustomerId, b.CustomerName);
        if (SelectedCustomer is not null && Customers.All(c => c.Id != SelectedCustomer.Id))
            Customers.Insert(0, SelectedCustomer);

        var day = Days.FirstOrDefault(d => d.Date == b.StartsAt.ToLocalTime().Date);
        if (day is not null)
        {
            foreach (var d in Days) d.IsSelected = d.Date == day.Date;
            SelectedDay = day;
            SelectedDayLabel = day.Date.ToString("dddd d MMMM yyyy");
            RebuildSelectedDayList();
        }
    }

    [RelayCommand]
    private void NewBooking()
    {
        SelectedBooking = null;
        IsEditing = false;
        PanelTitle = "New booking";
        PrepareFormForDay(SelectedDay?.Date ?? DateTime.Today);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Error = null;
        if (SelectedCustomer is null)
        {
            Error = "Select a customer.";
            return;
        }
        if (!DateTime.TryParse(BookingStartLocal, out var startLocal) ||
            !DateTime.TryParse(BookingEndLocal, out var endLocal))
        {
            Error = "Use start/end like 2026-09-28 14:00";
            return;
        }
        if (endLocal <= startLocal)
        {
            Error = "End must be after start.";
            return;
        }

        IsBusy = true;
        try
        {
            var start = new DateTimeOffset(DateTime.SpecifyKind(startLocal, DateTimeKind.Local));
            var end = new DateTimeOffset(DateTime.SpecifyKind(endLocal, DateTimeKind.Local));
            BookingDto saved;
            if (IsEditing && SelectedBooking is not null)
            {
                saved = await _api.PutAsync<UpdateBookingRequest, BookingDto>(
                    $"api/bookings/{SelectedBooking.Id}",
                    new UpdateBookingRequest(
                        SelectedCustomer.Id,
                        SelectedBooking.Booking.StaffId,
                        string.IsNullOrWhiteSpace(BookingType) ? "Intake" : BookingType.Trim(),
                        SelectedBooking.Booking.Status,
                        start,
                        end,
                        string.IsNullOrWhiteSpace(BookingNotes) ? null : BookingNotes.Trim()));
                StatusMessage = "Booking updated.";
            }
            else
            {
                saved = await _api.PostAsync<CreateBookingRequest, BookingDto>(
                    "api/bookings",
                    new CreateBookingRequest(
                        SelectedCustomer.Id,
                        null,
                        string.IsNullOrWhiteSpace(BookingType) ? "Intake" : BookingType.Trim(),
                        start,
                        end,
                        string.IsNullOrWhiteSpace(BookingNotes) ? null : BookingNotes.Trim()));
                StatusMessage = "Booking scheduled.";
            }

            await LoadBookingsForVisibleRangeAsync();
            RebuildGrid();
            var day = Days.FirstOrDefault(d => d.Date == saved.StartsAt.ToLocalTime().Date)
                ?? CreateTransientDay(saved.StartsAt.ToLocalTime().Date);
            SelectDay(day);
            var chip = SelectedDayBookings.FirstOrDefault(c => c.Id == saved.Id);
            if (chip is not null) SelectBooking(chip);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelBooking))]
    private async Task CancelBookingAsync()
    {
        if (SelectedBooking is null) return;
        Error = null;
        IsBusy = true;
        try
        {
            await _api.PostAsync<BookingDto>($"api/bookings/{SelectedBooking.Id}/cancel");
            StatusMessage = "Booking cancelled.";
            await LoadBookingsForVisibleRangeAsync();
            RebuildGrid();
            if (SelectedDay is not null) SelectDay(SelectedDay);
            NewBooking();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCancelBooking() =>
        SelectedBooking is not null && !SelectedBooking.IsCancelled;

    partial void OnSelectedBookingChanged(CalendarBookingChipVm? value) =>
        CancelBookingCommand.NotifyCanExecuteChanged();

    private async Task LoadCustomersAsync()
    {
        try
        {
            var page = await _api.GetAsync<PagedResult<CustomerListItemDto>>("api/customers?page=1&pageSize=100");
            var keepId = SelectedCustomer?.Id;
            Customers.Clear();
            foreach (var c in page.Items)
                Customers.Add(new ListPickItem(c.Id, $"{c.DisplayName} · {c.Phone ?? c.Email ?? c.Id.ToString()[..8]}"));
            if (keepId is Guid id)
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == id) ?? SelectedCustomer;
            else if (SelectedCustomer is null && Customers.Count > 0)
                SelectedCustomer = Customers[0];
        }
        catch
        {
            /* optional for view-only users */
        }
    }

    private async Task LoadBookingsForVisibleRangeAsync()
    {
        DateTime fromLocal;
        DateTime toLocalExclusive;
        if (IsWeekView)
        {
            fromLocal = VisibleWeekStart.Date;
            toLocalExclusive = fromLocal.AddDays(7);
            MonthTitle = $"{fromLocal:d MMM} – {toLocalExclusive.AddDays(-1):d MMM yyyy}";
        }
        else
        {
            fromLocal = VisibleMonth.AddDays(-(int)VisibleMonth.DayOfWeek);
            // 6 weeks grid
            toLocalExclusive = fromLocal.AddDays(42);
            MonthTitle = VisibleMonth.ToString("MMMM yyyy");
        }

        var from = new DateTimeOffset(DateTime.SpecifyKind(fromLocal, DateTimeKind.Local));
        var to = new DateTimeOffset(DateTime.SpecifyKind(toLocalExclusive, DateTimeKind.Local));
        // Use UTC Zulu form so query-string '+' offsets cannot be mis-decoded as spaces.
        var fromUtc = from.ToUniversalTime();
        var toUtc = to.ToUniversalTime();
        var list = await _api.GetAsync<BookingDto[]>(
            $"api/bookings?from={Uri.EscapeDataString(fromUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"))}&to={Uri.EscapeDataString(toUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"))}");
        _cache.Clear();
        _cache.AddRange(list ?? Array.Empty<BookingDto>());
    }

    private void RebuildGrid()
    {
        var keepDate = SelectedDay?.Date;
        Days.Clear();
        Weeks.Clear();

        DateTime gridStart;
        int dayCount;
        if (IsWeekView)
        {
            gridStart = VisibleWeekStart.Date;
            dayCount = 7;
        }
        else
        {
            gridStart = VisibleMonth.AddDays(-(int)VisibleMonth.DayOfWeek);
            dayCount = 42;
        }

        CalendarWeekVm? week = null;
        for (var i = 0; i < dayCount; i++)
        {
            if (i % 7 == 0)
            {
                week = new CalendarWeekVm();
                Weeks.Add(week);
            }

            var date = gridStart.AddDays(i);
            var day = new CalendarDayVm(date, IsWeekView || date.Month == VisibleMonth.Month);
            var dayBookings = _cache.Where(b =>
            {
                var local = b.StartsAt.ToLocalTime().Date;
                return local == date;
            });
            if (!ShowCancelled)
                dayBookings = dayBookings.Where(b => !string.Equals(b.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));
            day.SetBookings(dayBookings);
            Days.Add(day);
            week!.Days.Add(day);
        }

        _suppressDaySelection = true;
        try
        {
            if (keepDate is DateTime kd)
            {
                var match = Days.FirstOrDefault(d => d.Date == kd);
                if (match is not null)
                {
                    foreach (var d in Days) d.IsSelected = d.Date == kd;
                    SelectedDay = match;
                    RebuildSelectedDayList();
                }
            }
            else
            {
                var today = Days.FirstOrDefault(d => d.IsToday) ?? Days.FirstOrDefault();
                if (today is not null)
                {
                    foreach (var d in Days) d.IsSelected = d.Date == today.Date;
                    SelectedDay = today;
                    SelectedDayLabel = today.Date.ToString("dddd d MMMM yyyy");
                    PrepareFormForDay(today.Date);
                    RebuildSelectedDayList();
                }
            }
        }
        finally
        {
            _suppressDaySelection = false;
        }
    }

    private void RebuildSelectedDayList()
    {
        SelectedDayBookings.Clear();
        if (SelectedDay is null) return;
        foreach (var chip in SelectedDay.Bookings)
            SelectedDayBookings.Add(chip);
    }

    private void PrepareFormForDay(DateTime day)
    {
        var start = day.Date.AddHours(9);
        if (day.Date == DateTime.Today)
        {
            var nextHour = DateTime.Now.AddHours(1);
            start = new DateTime(day.Year, day.Month, day.Day, nextHour.Hour, 0, 0);
        }
        BookingStartLocal = start.ToString("yyyy-MM-dd HH:mm");
        BookingEndLocal = start.AddHours(1).ToString("yyyy-MM-dd HH:mm");
        if (string.IsNullOrWhiteSpace(BookingType)) BookingType = "Intake";
    }

    private static CalendarDayVm CreateTransientDay(DateTime date) =>
        new(date, true);

    private static DateTime StartOfWeek(DateTime date) =>
        date.Date.AddDays(-(int)date.DayOfWeek);

    partial void OnShowCancelledChanged(bool value)
    {
        if (Days.Count == 0) return;
        RebuildGrid();
    }
}
