using SmartTrip.Application.Services;

namespace Smart_tripp.WPF;

// Сервіси вікна «Інструменти подорожі».
public sealed record TripTools(
    BookingManagementService Bookings,
    ExpenseEditingService Expenses,
    VisitRescheduleService Visits,
    TripCopyService Copies,
    PackingTemplateService Templates,
    RecommendationService Recommendations,
    TripReportService Reports);
