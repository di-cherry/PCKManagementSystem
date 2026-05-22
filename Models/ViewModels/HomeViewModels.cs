namespace PCKManagementSystem.Models.ViewModels
{
    public class LandingPageViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalDocuments { get; set; }
        public int TotalTasks { get; set; }
        public int TotalDisciplines { get; set; }
        public List<Announcement> RecentAnnouncements { get; set; } = new();
    }

    public class DashboardViewModel
    {
        public string UserName { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;

        // Общая статистика
        public int TotalDocuments { get; set; }
        public int TotalTasks { get; set; }

        // Персональная статистика
        public int MyDocuments { get; set; }
        public int MyTasks { get; set; }
        public int MyPendingTasks { get; set; }

        // Последние элементы
        public List<Document> RecentDocuments { get; set; } = new();
        public List<Tasks> RecentTasks { get; set; } = new();
        public List<Announcement> Announcements { get; set; } = new();

        // Для прогресса по дисциплинам (список)
        public List<DisciplineProgressViewModel> DisciplineProgress { get; set; } = new();

        // Ближайшие задачи (на сегодня/завтра)
        public List<Tasks> UpcomingTasks { get; set; } = new();

        // Данные для графика
        public List<string> Months { get; set; } = new();
        public List<int> DocumentsCreated { get; set; } = new();
        public List<int> TasksCreated { get; set; } = new();
        public List<int> TasksCompleted { get; set; } = new();
    }
    public class DisciplineProgressViewModel
    {
        public string DisciplineName { get; set; } = string.Empty;
        public int TotalDocuments { get; set; }
        public int ApprovedDocuments { get; set; }
        public double ProgressPercent => TotalDocuments > 0 ? Math.Round((double)ApprovedDocuments / TotalDocuments * 100, 0) : 0;
    }
    public class AboutViewModel
    {
        public string SystemName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Features { get; set; } = new();
        public List<string> Technologies { get; set; } = new();
        public string Developer { get; set; } = string.Empty;
        public int Year { get; set; }
    }
    public class PrivacyViewModel
    {
        public DateTime LastUpdated { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
