namespace CalculatriceMaui;

public partial class MainPage : ContentPage
{
	private readonly CalculatorEngine _engine = new();
	private bool _layoutInitialized;
	private bool _isLandscape;
	private double _keyFontSize = 26;

	public MainPage()
	{
		InitializeComponent();

		// Un ScrollView horizontal mesure son contenu sans limite de largeur : on impose donc
		// au Label une largeur minimale égale à la zone visible pour que le texte reste aligné à droite.
		ResultScroll.SizeChanged += (_, _) => ResultLabel.MinimumWidthRequest = ResultScroll.Width;
		ExpressionScroll.SizeChanged += (_, _) => ExpressionLabel.MinimumWidthRequest = ExpressionScroll.Width;

		Refresh();
	}

	// ------------------------------------------------------------ orientation

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		if (width <= 0 || height <= 0) return;

		var landscape = width > height;
		if (_layoutInitialized && landscape == _isLandscape) return;

		_layoutInitialized = true;
		_isLandscape = landscape;
		ApplyLayout();
	}

	private void ApplyLayout()
	{
		RootGrid.RowDefinitions.Clear();
		RootGrid.ColumnDefinitions.Clear();

		if (_isLandscape)
		{
			// Paysage : affichage à gauche (2/5), clavier à droite (3/5).
			RootGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
			RootGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(3, GridUnitType.Star)));
			RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

			Grid.SetRow(DisplayBorder, 0);
			Grid.SetColumn(DisplayBorder, 0);
			Grid.SetRow(KeypadGrid, 0);
			Grid.SetColumn(KeypadGrid, 1);
			_keyFontSize = 20;
		}
		else
		{
			// Portrait : affichage en haut (2/7), clavier en bas (5/7).
			RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			RootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(2, GridUnitType.Star)));
			RootGrid.RowDefinitions.Add(new RowDefinition(new GridLength(5, GridUnitType.Star)));

			Grid.SetRow(DisplayBorder, 0);
			Grid.SetColumn(DisplayBorder, 0);
			Grid.SetRow(KeypadGrid, 1);
			Grid.SetColumn(KeypadGrid, 0);
			_keyFontSize = 26;
		}

		foreach (var button in KeypadGrid.Children.OfType<Button>())
			button.FontSize = button.Text.Length > 1 && button.Text != "⌫" ? _keyFontSize - 4 : _keyFontSize;

		Refresh();
	}

	// ------------------------------------------------------------ gestionnaires

	private void OnDigitClicked(object? sender, EventArgs e)
	{
		if (sender is Button { Text: { Length: > 0 } text }) _engine.InputDigit(text[0]);
		Refresh();
	}

	private void OnDecimalClicked(object? sender, EventArgs e) { _engine.InputDecimalSeparator(); Refresh(); }

	private void OnOperatorClicked(object? sender, EventArgs e)
	{
		if (sender is Button { Text: { Length: > 0 } text }) _engine.SetOperator(text[0]);
		Refresh();
	}

	private void OnEqualsClicked(object? sender, EventArgs e) { _engine.Evaluate(); Refresh(); }
	private void OnClearClicked(object? sender, EventArgs e) { _engine.Clear(); Refresh(); }
	private void OnBackspaceClicked(object? sender, EventArgs e) { _engine.Backspace(); Refresh(); }
	private void OnToggleSignClicked(object? sender, EventArgs e) { _engine.ToggleSign(); Refresh(); }
	private void OnPercentClicked(object? sender, EventArgs e) { _engine.Percent(); Refresh(); }
	private void OnSquareRootClicked(object? sender, EventArgs e) { _engine.SquareRoot(); Refresh(); }
	private void OnSquareClicked(object? sender, EventArgs e) { _engine.Square(); Refresh(); }
	private void OnReciprocalClicked(object? sender, EventArgs e) { _engine.Reciprocal(); Refresh(); }

	// ------------------------------------------------------------ affichage

	private void Refresh()
	{
		ResultLabel.Text = _engine.DisplayText;
		ExpressionLabel.Text = _engine.Expression;
		ErrorBadge.IsVisible = _engine.HasError;
		ResultLabel.FontSize = ResultFontSize(ResultLabel.Text.Length);
		ExpressionLabel.FontSize = _isLandscape ? 15 : 18;

		// Garde la fin du texte visible quand il dépasse la largeur disponible.
		Dispatcher.Dispatch(async () =>
		{
			await ResultScroll.ScrollToAsync(ResultLabel, ScrollToPosition.End, false);
			await ExpressionScroll.ScrollToAsync(ExpressionLabel, ScrollToPosition.End, false);
		});
	}

	/// <summary>Réduit la police quand le texte s'allonge, pour éviter qu'il déborde sur petit écran.</summary>
	private double ResultFontSize(int length)
	{
		var baseSize = _isLandscape ? 40.0 : 52.0;
		return length switch
		{
			<= 8 => baseSize,
			<= 11 => baseSize * 0.8,
			<= 14 => baseSize * 0.65,
			_ => baseSize * 0.5
		};
	}
}
