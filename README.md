# Calculatrice MAUI

Calculatrice mobile en .NET MAUI (projet Single Project) : Android, iOS et Windows.

## Prérequis
- SDK .NET 9 et workload MAUI : `dotnet workload install maui`
- Android : émulateur ou appareil ; iOS : Mac avec Xcode ; Windows : Windows 10/11

## Lancer
```
dotnet build -t:Run -f net9.0-android
dotnet build -t:Run -f net9.0-windows10.0.19041.0
```

## Structure
| Fichier | Rôle |
|---|---|
| `MainPage.xaml` | Interface : Grid, Border, VerticalStackLayout, HorizontalStackLayout, ScrollView |
| `MainPage.xaml.cs` | Gestionnaires d'événements, adaptation portrait/paysage, taille de police |
| `CalculatorEngine.cs` | Logique de calcul (decimal), gestion des erreurs |
| `Resources/Styles/` | Couleurs (thèmes clair/sombre) et styles des touches |

