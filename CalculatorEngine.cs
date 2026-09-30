using System.Globalization;

namespace CalculatriceMaui;

/// <summary>
/// Logique de calcul, indépendante de l'interface : la page ne fait que relayer les touches
/// et afficher <see cref="DisplayText"/> et <see cref="Expression"/>.
/// Toutes les valeurs sont manipulées en <see cref="decimal"/> pour éviter les erreurs
/// d'arrondi binaire (0,1 + 0,2 donne bien 0,3).
/// </summary>
public sealed class CalculatorEngine
{
	private const int MaxDigits = 15;     // longueur maximale d'un nombre saisi
	private const int MaxDecimals = 10;   // précision conservée pour les résultats

	private decimal? _left;               // opérande de gauche en attente
	private char? _operator;              // opérateur en attente (+ − × ÷)
	private bool _startNewEntry = true;   // la prochaine touche chiffre remplace l'affichage
	private bool _awaitingOperand;        // un opérateur vient d'être pressé, pas encore d'opérande droit
	private bool _justEvaluated;          // le dernier appui était « = » (ou une fonction sans opérateur)
	private bool _hasError;

	/// <summary>Nombre en cours de saisie, au format technique (point décimal).</summary>
	public string Current { get; private set; } = "0";

	/// <summary>Opération en cours, affichée au-dessus du résultat.</summary>
	public string Expression { get; private set; } = "";

	public string ErrorMessage { get; private set; } = "";
	public bool HasError => _hasError;

	/// <summary>Texte à montrer dans la zone principale (virgule décimale à la française).</summary>
	public string DisplayText => _hasError ? ErrorMessage : Current.Replace('.', ',');

	// ---------------------------------------------------------------- saisie

	public void InputDigit(char digit)
	{
		RecoverFromError();
		ForgetFinishedExpression();
		_awaitingOperand = false;

		if (_startNewEntry)
		{
			Current = digit.ToString();
			_startNewEntry = false;
			return;
		}

		if (DigitCount(Current) >= MaxDigits) return;

		Current = Current switch
		{
			"0" => digit.ToString(),
			"-0" => "-" + digit,
			_ => Current + digit
		};
	}

	public void InputDecimalSeparator()
	{
		RecoverFromError();
		ForgetFinishedExpression();
		_awaitingOperand = false;

		if (_startNewEntry)
		{
			Current = "0.";
			_startNewEntry = false;
			return;
		}

		if (!Current.Contains('.') && DigitCount(Current) < MaxDigits)
			Current += ".";
	}

	public void Clear()
	{
		_left = null;
		_operator = null;
		_startNewEntry = true;
		_awaitingOperand = false;
		_justEvaluated = false;
		_hasError = false;
		ErrorMessage = "";
		Current = "0";
		Expression = "";
	}

	public void Backspace()
	{
		if (_hasError) { Clear(); return; }
		if (_startNewEntry) return;   // on n'efface pas un résultat calculé

		Current = Current[..^1];
		if (Current is "" or "-" or "-0") Current = "0";
	}

	public void ToggleSign()
	{
		RecoverFromError();

		if (_awaitingOperand)
		{
			// « 5 + ± » : on commence la saisie d'un opérande négatif
			Current = "-0";
			_startNewEntry = false;
			_awaitingOperand = false;
			return;
		}

		if (Current == "0") return;
		Current = Current.StartsWith('-') ? Current[1..] : "-" + Current;
	}

	public void Percent()
	{
		RecoverFromError();

		var value = Parse(Current);
		// 200 + 10 %  ->  10 % de 200 ;  partout ailleurs : valeur / 100
		var result = _left.HasValue && _operator is '+' or '−'
			? _left.Value * value / 100
			: value / 100;

		Current = Format(Normalize(result));
		_startNewEntry = true;
		_awaitingOperand = false;
	}

	// ---------------------------------------------------------------- opérations

	public void SetOperator(char op)
	{
		RecoverFromError();

		if (_awaitingOperand && _operator.HasValue && _left.HasValue)
		{
			// Changement d'avis : « 5 + » puis « × » remplace simplement l'opérateur.
			_operator = op;
			Expression = $"{Pretty(_left.Value)} {op}";
			return;
		}

		var current = Parse(Current);

		if (_operator.HasValue && _left.HasValue)
		{
			// Enchaînement : « 2 + 3 × » calcule d'abord 2 + 3.
			var pending = $"{Pretty(_left.Value)} {_operator} {Pretty(current)}";
			if (!TryCompute(_left.Value, _operator.Value, current, pending, out var partial)) return;
			_left = partial;
		}
		else
		{
			_left = current;
		}

		_operator = op;
		Current = Format(_left.Value);
		Expression = $"{Pretty(_left.Value)} {op}";
		_startNewEntry = true;
		_awaitingOperand = true;
		_justEvaluated = false;
	}

	public void Evaluate()
	{
		RecoverFromError();
		if (!_operator.HasValue || !_left.HasValue) return;

		// « 5 + = » réutilise 5 comme opérande droit, comme sur une calculatrice classique.
		var right = _awaitingOperand ? _left.Value : Parse(Current);
		var expression = $"{Pretty(_left.Value)} {_operator} {Pretty(right)} =";

		if (!TryCompute(_left.Value, _operator.Value, right, expression, out var result)) return;

		Expression = expression;
		Current = Format(result);
		_left = null;
		_operator = null;
		_startNewEntry = true;
		_awaitingOperand = false;
		_justEvaluated = true;
	}

	public void SquareRoot()
	{
		RecoverFromError();
		var value = Parse(Current);
		var label = $"√({Pretty(value)})";

		if (value < 0) { Fail("Entrée invalide", label); return; }
		ApplyUnary(label, (decimal)Math.Sqrt((double)value));
	}

	public void Square()
	{
		RecoverFromError();
		var value = Parse(Current);
		var label = $"{Pretty(value)}²";

		try { ApplyUnary(label, checked(value * value)); }
		catch (OverflowException) { Fail("Résultat trop grand", label); }
	}

	public void Reciprocal()
	{
		RecoverFromError();
		var value = Parse(Current);
		var label = $"1 ÷ {Pretty(value)}";

		if (value == 0) { Fail("Division par zéro impossible", label); return; }
		ApplyUnary(label, 1m / value);
	}

	// ---------------------------------------------------------------- interne

	private void ApplyUnary(string label, decimal result)
	{
		var prefix = _operator.HasValue && _left.HasValue ? $"{Pretty(_left.Value)} {_operator} " : "";
		Expression = prefix + label;
		Current = Format(Normalize(result));
		_startNewEntry = true;
		_awaitingOperand = false;
		_justEvaluated = !_operator.HasValue;
	}

	private bool TryCompute(decimal a, char op, decimal b, string expression, out decimal result)
	{
		result = 0;
		try
		{
			switch (op)
			{
				case '+': result = checked(a + b); break;
				case '−': result = checked(a - b); break;
				case '×': result = checked(a * b); break;
				case '÷':
					if (b == 0)
					{
						Fail("Division par zéro impossible", expression);
						return false;
					}
					result = a / b;
					break;
			}
			result = Normalize(result);
			return true;
		}
		catch (OverflowException)
		{
			Fail("Résultat trop grand", expression);
			return false;
		}
	}

	private void Fail(string message, string expression)
	{
		_hasError = true;
		ErrorMessage = message;
		Expression = expression;
		Current = "0";
		_left = null;
		_operator = null;
		_startNewEntry = true;
		_awaitingOperand = false;
		_justEvaluated = false;
	}

	/// <summary>Après une erreur, la touche suivante repart d'un état propre.</summary>
	private void RecoverFromError()
	{
		if (_hasError) Clear();
	}

	/// <summary>Taper un chiffre après « = » démarre un nouveau calcul.</summary>
	private void ForgetFinishedExpression()
	{
		if (!_justEvaluated) return;
		Expression = "";
		_justEvaluated = false;
	}

	private static decimal Normalize(decimal value)
		=> Math.Round(value, MaxDecimals, MidpointRounding.AwayFromZero);

	private static decimal Parse(string text)
		=> decimal.Parse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

	private static string Format(decimal value)
		=> value == 0 ? "0" : value.ToString("0.##########", CultureInfo.InvariantCulture);

	/// <summary>Nombre lisible dans l'expression : virgule, et parenthèses si négatif.</summary>
	private static string Pretty(decimal value)
	{
		var text = Format(value).Replace('.', ',');
		return value < 0 ? $"({text})" : text;
	}

	private static int DigitCount(string text) => text.Count(char.IsDigit);
}
