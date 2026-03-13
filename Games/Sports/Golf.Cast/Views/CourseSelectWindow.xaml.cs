using GolfCast.Models;
using GolfCast.Services;

namespace GolfCast.Views;

public partial class CourseSelectWindow : Window
{
    public CourseSet? SelectedCourse { get; private set; }

    public CourseSelectWindow()
    {
        InitializeComponent();
        LstCourses.ItemsSource = CourseData.AllSets;
        LstCourses.SelectedIndex = 0;
    }

    private void OnStart(object sender, RoutedEventArgs e)
    {
        if (LstCourses.SelectedItem is CourseSet c)
        {
            SelectedCourse = c;
            DialogResult   = true;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
    private void OnDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => OnStart(sender, e);
}
