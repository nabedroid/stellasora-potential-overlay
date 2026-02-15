using System.Collections.Generic;
using System.Windows;
using StellasoraPotentialOverlay.Models;

namespace StellasoraPotentialOverlay;

public partial class DebugWindow : Window
{
    public DebugWindow()
    {
        InitializeComponent();
    }

    public void UpdateDebugInfo(List<OcrDebugResult> results)
    {
        // UIスレッドで更新
        Dispatcher.Invoke(() =>
        {
            DebugResultsDataGrid.ItemsSource = results;
        });
    }
}
