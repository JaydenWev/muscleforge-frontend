using System.Text.Json;
using Microsoft.Maui.Controls.Shapes;

namespace MuscleForge.Mobile.Views;

// The revamped app keeps its data on this device; it does not require an account or server.
public sealed class RevampedPage : ContentPage
{
    static readonly Color PageColor = Color.FromArgb("#14171E"), Surface = Color.FromArgb("#22262F"), Raised = Color.FromArgb("#303541"), Accent = Color.FromArgb("#E66400"), Muted = Color.FromArgb("#ADB1BD");
    const string StorageKey = "revamped.training.v1";
    readonly TrainingData data;
    readonly Grid root = new();
    VerticalStackLayout body = new() { Spacing = 16, Padding = new Thickness(20, 18, 20, 24) };
    string tab = "Home";
    Workout? selected;
    DateTime started;
    readonly List<LoggedSet> sets = [];
    IDispatcherTimer? timer;

    public RevampedPage()
    {
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = PageColor;
        try { data = JsonSerializer.Deserialize<TrainingData>(Preferences.Default.Get(StorageKey, "")) ?? new(); }
        catch (JsonException) { data = new(); }
        Content = root;
        if (!data.Onboarded) Onboarding(); else ShowTab("Home");
    }

    void Save() => Preferences.Default.Set(StorageKey, JsonSerializer.Serialize(data));
    static Label Text(string text, double size = 14, bool bold = false, Color? color = null) => new() { Text = text, FontSize = size, FontFamily = bold ? "ManropeExtraBold" : "Manrope", TextColor = color ?? Colors.White, LineHeight = 1.2 };
    static Button Button(string text, Action action, bool primary = false) { var b = new Button { Text = text, BackgroundColor = primary ? Accent : Surface, TextColor = primary ? PageColor : Colors.White, CornerRadius = 12, MinimumHeightRequest = 48, Padding = new Thickness(12, 8), FontFamily = "ManropeSemiBold", FontSize = 13 }; b.Clicked += (_, _) => action(); return b; }
    static Border Card(View content, Color? color = null) => new() { BackgroundColor = color ?? Surface, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 14 }, Padding = 16, Content = content };
    static VerticalStackLayout Stack(params View[] children) { var s = new VerticalStackLayout { Spacing = 8 }; foreach (var v in children) s.Children.Add(v); return s; }
    static Grid Row(View left, View right) { var g = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 }; g.Add(left); g.Add(right, 1); return g; }
    Action? dismissDialog;

    protected override bool OnBackButtonPressed()
    {
        if (dismissDialog is null) return base.OnBackButtonPressed();
        dismissDialog();
        return true;
    }

    Task<string?> DialogAsync(string title, string? message, string cancel, string[] actions, Entry? entry = null)
    {
        var completion = new TaskCompletionSource<string?>();
        var panel = new VerticalStackLayout { Spacing = 16 };
        panel.Children.Add(Text(title, 20, true));
        if (!string.IsNullOrWhiteSpace(message)) panel.Children.Add(Text(message, 14, color: Muted));
        if (entry != null) panel.Children.Add(new Border
        {
            BackgroundColor = Raised, StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(12, 0), Content = entry
        });
        var overlay = new Grid { BackgroundColor = Color.FromRgba(0, 0, 0, 0.72), Padding = 24 };
        var card = Card(panel);
        card.Stroke = new SolidColorBrush(Color.FromArgb("#414654"));
        card.StrokeThickness = 1;
        card.HorizontalOptions = LayoutOptions.Fill;
        card.VerticalOptions = LayoutOptions.Center;
        card.MaximumWidthRequest = 440;
        overlay.Children.Add(card);
        void Close(string? result)
        {
            if (completion.Task.IsCompleted) return;
            dismissDialog = null;
            entry?.Unfocus();
            root.Children.Remove(overlay);
            completion.TrySetResult(result);
        }
        foreach (var action in actions)
        {
            var result = action;
            panel.Children.Add(Button(action, () => Close(result), primary: true));
        }
        panel.Children.Add(Button(cancel, () => Close(null)));
        dismissDialog = () => Close(null);
        Grid.SetRowSpan(overlay, root.RowDefinitions.Count);
        root.Children.Add(overlay);
        if (entry != null) Dispatcher.Dispatch(() => entry.Focus());
        return completion.Task;
    }

    async Task<string?> PromptAsync(string title, string message, string accept, string cancel,
        int maxLength = -1, Keyboard? keyboard = null, string? initialValue = null)
    {
        var entry = new Entry
        {
            AutomationId = "StyledDialogInput",
            Text = initialValue, TextColor = Colors.White, PlaceholderColor = Muted,
            FontFamily = "Manrope", FontSize = 15, Keyboard = keyboard ?? Keyboard.Default,
            MaxLength = maxLength < 0 ? int.MaxValue : maxLength,
            BackgroundColor = Colors.Transparent
        };
        var result = await DialogAsync(title, message, cancel, [accept], entry);
        return result == accept ? entry.Text : null;
    }

    async Task<bool> AlertAsync(string title, string message, string accept, string cancel) =>
        await DialogAsync(title, message, cancel, [accept]) == accept;

    async Task AlertAsync(string title, string message, string accept) =>
        _ = await DialogAsync(title, message, accept, []);

    Task<string?> ActionSheetAsync(string title, string cancel, string? destruction, params string[] actions) =>
        DialogAsync(title, null, cancel, actions);

    void Page(string title, bool navigation = true, Action? back = null)
    {
        root.Children.Clear(); root.RowDefinitions.Clear();
        body = new VerticalStackLayout { Spacing = 16, Padding = new Thickness(20, 18, 20, 24), VerticalOptions = LayoutOptions.Start };
        root.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        if (navigation) root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var scroll = new ScrollView { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Never };
        root.Add(scroll);
        if (back != null) body.Children.Add(Row(Text(title, 23, true), Button("← Back", back)));
        else body.Children.Add(Text(title, 25, true));
        if (!navigation) return;
        var bar = new Grid { BackgroundColor = Surface, Padding = new Thickness(4, 6), ColumnSpacing = 1 };
        string[] labels = ["Home", "Progress", "Workout", "Exercises", "Profile"];
        string[] icons = ["⌂", "▥", "▣", "◈", "◉"];
        for (var i = 0; i < labels.Length; i++)
        {
            bar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var name = labels[i]; var item = new VerticalStackLayout { Spacing = 1, HorizontalOptions = LayoutOptions.Fill, Children = { new Label { Text = icons[i], FontSize = 20, TextColor = tab == name ? Accent : Muted, HorizontalTextAlignment = TextAlignment.Center }, new Label { Text = name, FontSize = 9, TextColor = tab == name ? Accent : Muted, HorizontalTextAlignment = TextAlignment.Center } } };
            var tap = new TapGestureRecognizer(); tap.Tapped += (_, _) => ShowTab(name); item.GestureRecognizers.Add(tap); bar.Add(item, i);
        }
        root.Add(bar, 0, 1);
    }
    void Onboarding()
    {
        Page("", false);
        body.VerticalOptions = LayoutOptions.Center;
        body.Children.Add(new Label { Text = "◀━●━▶", TextColor = Accent, FontSize = 58, HorizontalTextAlignment = TextAlignment.Center });
        body.Children.Add(new Label { Text = "MuscleForge", TextColor = Colors.White, FontSize = 32, FontFamily = "ManropeExtraBold", HorizontalTextAlignment = TextAlignment.Center });
        body.Children.Add(new Label { Text = "Track workouts, build strength, and see progress.", TextColor = Muted, FontSize = 16, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(20, 12, 20, 50) });
        body.Children.Add(Button("Get Started", () => { data.Onboarded = true; Save(); body.VerticalOptions = LayoutOptions.Start; ShowTab("Home"); }, true));
    }
    void ShowTab(string name)
    {
        timer?.Stop(); tab = name; body.VerticalOptions = LayoutOptions.Start;
        switch (name) { case "Home": Home(); break; case "Progress": Progress(); break; case "Workout": Workouts(); break; case "Exercises": Exercises(); break; case "Profile": Profile(); break; }
    }
    void Home()
    {
        Page("Today");
        body.Children.Add(Text(DateTime.Now.ToString("dddd, MMMM d"), 13, color: Muted));
        var latest = data.Workouts.FirstOrDefault();
        body.Children.Add(Card(Stack(Text(latest?.Name ?? "Your next workout", 18, true), Text(latest == null ? "Create a workout to get started." : $"{latest.Exercises.Count} exercises", 12, color: Muted), Button(latest == null ? "Create workout" : "View workout  →", () => { if (latest == null) CreateWorkout(); else WorkoutDetail(latest); }, true)), Raised));
        body.Children.Add(Text("This Week", 18, true));
        var week = data.Sessions.Where(s => s.Finished >= DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7))).ToList();
        body.Children.Add(Card(Row(Stack(Text(week.Count.ToString(), 22, true), Text("Workouts", 12, color: Muted)), Stack(Text(week.Sum(s => s.Sets.Count).ToString(), 22, true), Text("Sets", 12, color: Muted)))));
        body.Children.Add(Text("Goals", 18, true));
        body.Children.Add(Card(Stack(Row(Text("Workouts per week", 14, true), Text($"{week.Count} / {data.Goal}", 13)), new ProgressBar { Progress = Math.Min(1, (double)week.Count / Math.Max(1, data.Goal)), ProgressColor = Accent })));
        body.Children.Add(Button("Social", Social));
    }
    async void CreateWorkout()
    {
        var name = await PromptAsync("Create workout", "Give your workout a name", "Create", "Cancel", maxLength: 80);
        if (string.IsNullOrWhiteSpace(name)) return;
        var workout = new Workout { Name = name.Trim() }; data.Workouts.Add(workout); Save(); WorkoutDetail(workout);
    }
    void Workouts()
    {
        Page("Workouts");
        body.Children.Add(Button("＋ Create Workout", CreateWorkout, true));
        body.Children.Add(Button("Browse exercises", () => ShowTab("Exercises")));
        if (data.Workouts.Count == 0) body.Children.Add(Card(Text("Your workouts will appear here. Create one and add exercises to get started.", 14, color: Muted)));
        foreach (var w in data.Workouts.ToList()) body.Children.Add(Card(Row(Stack(Text(w.Name, 16, true), Text($"{w.Exercises.Count} exercises", 12, color: Muted)), Button("Open  ›", () => WorkoutDetail(w)))));
    }
    void WorkoutDetail(Workout workout)
    {
        selected = workout; Page(workout.Name, true, Workouts);
        body.Children.Add(Button("▶  Start Workout", () => StartWorkout(workout), true));
        body.Children.Add(Text($"{workout.Exercises.Count} exercises", 13, color: Muted));
        foreach (var exercise in workout.Exercises.ToList()) body.Children.Add(Card(Row(Stack(Text(exercise, 15, true), Text("Tap to view exercise", 12, color: Muted)), Button("⋯", async () => { var choice = await ActionSheetAsync(exercise, "Cancel", null, "View details", "Remove"); if (choice == "Remove") { workout.Exercises.Remove(exercise); Save(); WorkoutDetail(workout); } else if (choice == "View details") ExerciseDetail(exercise, () => WorkoutDetail(workout)); }))));
        body.Children.Add(Button("＋ Add exercise", () => Exercises(workout)));
        body.Children.Add(Button("Delete workout", async () => { if (await AlertAsync("Delete workout?", "This removes the workout from your library.", "Delete", "Cancel")) { data.Workouts.Remove(workout); Save(); Workouts(); } }));
    }
    static readonly string[] Catalog = ["Bench Press", "Incline Bench Press", "Chest Fly", "Push Up", "Dip", "Bent Over Row", "Lat Pulldown", "Pull Up", "Overhead Press", "Lateral Raise", "Squat", "Deadlift", "Leg Press", "Lunge", "Biceps Curl", "Triceps Extension"];
    static string Group(string name) => name switch { "Bench Press" or "Incline Bench Press" or "Chest Fly" or "Push Up" or "Dip" => "Chest", "Bent Over Row" or "Lat Pulldown" or "Pull Up" or "Deadlift" => "Back", "Squat" or "Leg Press" or "Lunge" => "Legs", "Overhead Press" or "Lateral Raise" => "Shoulders", _ => "Arms" };
    void Exercises(Workout? adding = null)
    {
        Page(adding == null ? "Exercises" : "Add exercise", true, adding == null ? null : () => WorkoutDetail(adding));
        var search = new SearchBar { Placeholder = "Search exercises...", TextColor = Colors.White, PlaceholderColor = Muted, BackgroundColor = Surface };
        var list = new VerticalStackLayout { Spacing = 8 }; string filter = "All";
        void Refresh() { list.Children.Clear(); foreach (var item in Catalog.Where(x => (filter == "All" || Group(x) == filter) && x.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase))) { var exercise = item; list.Children.Add(Card(Row(Stack(Text(exercise, 14, true), Text(Group(exercise), 12, color: Muted)), Button(adding == null ? "›" : "+", () => { if (adding == null) ExerciseDetail(exercise, () => Exercises()); else { adding.Exercises.Add(exercise); Save(); WorkoutDetail(adding); } })))); } }
        search.TextChanged += (_, _) => Refresh(); body.Children.Add(search);
        var filters = new HorizontalStackLayout { Spacing = 6 }; foreach (var group in new[] { "All", "Chest", "Back", "Shoulders", "Arms", "Legs" }) { var g = group; filters.Children.Add(Button(g, () => { filter = g; Refresh(); })); }
        body.Children.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = filters, HorizontalScrollBarVisibility = ScrollBarVisibility.Never }); body.Children.Add(list); Refresh();
    }
    void ExerciseDetail(string exercise, Action back)
    {
        Page(exercise, true, back); body.Children.Add(Text(Group(exercise), 13, color: Accent));
        body.Children.Add(Card(new Label { Text = "🏋", FontSize = 72, HorizontalTextAlignment = TextAlignment.Center }));
        body.Children.Add(Text("Exercise", 18, true)); body.Children.Add(Text("Use a controlled range of motion and choose a weight appropriate for your ability.", 14, color: Muted));
    }
    void StartWorkout(Workout workout) { if (workout.Exercises.Count == 0) { _ = AlertAsync("Add an exercise", "Add at least one exercise before starting.", "OK"); return; } selected = workout; sets.Clear(); started = DateTime.Now; LogWorkout(); }
    void LogWorkout()
    {
        if (selected == null) return; var workout = selected; Page(workout.Name, false, () => WorkoutDetail(workout));
        body.Children.Add(Text("Log your sets as you train.", 13, color: Muted));
        foreach (var name in workout.Exercises.Distinct())
        {
            var exercise = name; var section = Stack(Text(exercise, 17, true));
            foreach (var set in sets.Where(s => s.Exercise == exercise).ToList()) section.Children.Add(Row(Text($"{set.Weight:g} kg  ×  {set.Reps} reps", 14), Button("Remove", () => { sets.Remove(set); LogWorkout(); })));
            section.Children.Add(Button("＋ Add set", async () => { var weight = await PromptAsync("Add set", "Weight in kg", "Next", "Cancel", keyboard: Keyboard.Numeric); if (!double.TryParse(weight, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out var kg) || kg < 0) return; var reps = await PromptAsync("Add set", "Repetitions", "Save", "Cancel", keyboard: Keyboard.Numeric); if (!int.TryParse(reps, out var count) || count <= 0) return; sets.Add(new LoggedSet { Exercise = exercise, Weight = kg, Reps = count }); LogWorkout(); }));
            body.Children.Add(Card(section));
        }
        body.Children.Add(Button("Rest timer", Rest));
        body.Children.Add(Button("Finish workout", async () => { if (sets.Count == 0) { await AlertAsync("No sets logged", "Add a set before finishing.", "OK"); return; } data.Sessions.Add(new Session { Name = workout.Name, Finished = DateTime.Now, DurationMinutes = Math.Max(1, (int)(DateTime.Now - started).TotalMinutes), Sets = sets.ToList() }); Save(); sets.Clear(); ShowTab("Home"); }, true));
        body.Children.Add(Button("Discard workout", async () => { if (await AlertAsync("Discard workout?", "Your logged sets will be lost.", "Discard", "Keep training")) { sets.Clear(); WorkoutDetail(workout); } }));
    }
    void Rest()
    {
        Page("Rest", false, () => { timer?.Stop(); LogWorkout(); }); var remaining = 90; var label = new Label { Text = "1:30", FontSize = 56, FontFamily = "ManropeExtraBold", TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 70) }; body.Children.Add(label);
        timer?.Stop(); timer = Dispatcher.CreateTimer(); timer.Interval = TimeSpan.FromSeconds(1); timer.Tick += (_, _) => { if (remaining > 0) remaining--; label.Text = $"{remaining / 60}:{remaining % 60:00}"; if (remaining == 0) timer.Stop(); }; timer.Start();
        body.Children.Add(Button("＋ 30 seconds", () => { remaining += 30; label.Text = $"{remaining / 60}:{remaining % 60:00}"; if (!timer.IsRunning) timer.Start(); }));
        body.Children.Add(Button("Done", () => { timer.Stop(); LogWorkout(); }, true));
    }
    void History()
    {
        Page("History", true, () => ShowTab(tab));
        if (data.Sessions.Count == 0) body.Children.Add(Card(Text("Completed workouts will appear here.", 14, color: Muted)));
        foreach (var session in data.Sessions.OrderByDescending(s => s.Finished)) { var s = session; body.Children.Add(Card(Row(Stack(Text(s.Finished.ToString("ddd, MMM d"), 14, true), Text($"{s.Name} · {s.Sets.Count} sets", 12, color: Muted)), Button($"{s.DurationMinutes} min  ›", () => SessionDetail(s))))); }
    }
    void SessionDetail(Session session) { Page(session.Name, true, History); body.Children.Add(Text(session.Finished.ToString("f"), 13, color: Muted)); foreach (var group in session.Sets.GroupBy(s => s.Exercise)) body.Children.Add(Card(Stack(Text(group.Key, 16, true), Text(string.Join("\n", group.Select(s => $"{s.Weight:g} kg × {s.Reps}")), 14, color: Muted)))); }
    void Progress()
    {
        Page("Progress"); var volume = data.Sessions.SelectMany(s => s.Sets).Sum(s => s.Weight * s.Reps); body.Children.Add(Card(Stack(Text("Total Volume", 13), Text($"{volume:N0} kg", 27, true), Text("All time", 12, color: Muted))));
        body.Children.Add(Text("Your training", 18, true)); body.Children.Add(Card(Stack(Text($"{data.Sessions.Count} workouts", 16, true), Text($"{data.Sessions.Sum(s => s.Sets.Count)} sets completed", 13, color: Muted)))); body.Children.Add(Button("View history  ›", History));
    }
    void Social() { Page("Social", true, () => ShowTab("Home")); body.Children.Add(Card(Stack(Text("Train together", 18, true), Text("Social features need an online service. Your workouts currently stay on this device.", 14, color: Muted)))); }
    void Profile()
    {
        Page("Profile"); body.Children.Add(new Label { Text = "◉", FontSize = 60, TextColor = Accent, HorizontalTextAlignment = TextAlignment.Center }); body.Children.Add(new Label { Text = data.Name, FontSize = 22, FontFamily = "ManropeExtraBold", TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center });
        body.Children.Add(Card(Row(Stack(Text(data.Sessions.Count.ToString(), 20, true), Text("Workouts", 12, color: Muted)), Stack(Text($"{data.Sessions.Sum(s => s.DurationMinutes)}m", 20, true), Text("Total time", 12, color: Muted)))));
        body.Children.Add(Button("Edit name  ›", async () => { var name = await PromptAsync("Your name", "Name", "Save", "Cancel", initialValue: data.Name, maxLength: 60); if (!string.IsNullOrWhiteSpace(name)) { data.Name = name.Trim(); Save(); Profile(); } }));
        body.Children.Add(Button($"Weekly goal: {data.Goal} workouts  ›", async () => { var value = await PromptAsync("Weekly goal", "Workouts per week (1–7)", "Save", "Cancel", keyboard: Keyboard.Numeric); if (int.TryParse(value, out var goal) && goal is >= 1 and <= 7) { data.Goal = goal; Save(); Profile(); } }));
        body.Children.Add(Button("History  ›", History));
    }
    public sealed class TrainingData { public bool Onboarded { get; set; } public string Name { get; set; } = "Athlete"; public int Goal { get; set; } = 4; public List<Workout> Workouts { get; set; } = []; public List<Session> Sessions { get; set; } = []; }
    public sealed class Workout { public string Name { get; set; } = ""; public List<string> Exercises { get; set; } = []; }
    public sealed class Session { public string Name { get; set; } = ""; public DateTime Finished { get; set; } public int DurationMinutes { get; set; } public List<LoggedSet> Sets { get; set; } = []; }
    public sealed class LoggedSet { public string Exercise { get; set; } = ""; public double Weight { get; set; } public int Reps { get; set; } }
}
