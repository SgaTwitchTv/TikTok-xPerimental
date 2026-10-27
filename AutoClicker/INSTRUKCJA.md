# Autoclicker dla Windows

Uruchom `AutoClicker.exe` z katalogu `artifacts/AutoClicker-win-arm64` (ten komputer)
lub `artifacts/AutoClicker-win-x64` (komputer Intel/AMD).
Wariant przenośny wymaga zainstalowanego .NET Desktop Runtime 10 odpowiedniej architektury.
Przygotowane warianty EXE obsługują Windows ARM64 oraz x64.

1. Ustaw suwakiem od 2 do 100 kliknięć na sekundę.
2. Ustaw kursor w miejscu docelowym i naciśnij **F6**. Można również użyć przycisku Start.
3. Naciśnij **F6**, aby zatrzymać. Zamknięcie okna również anuluje pracę.

Program klika lewym przyciskiem w bieżącej pozycji kursora. F6 działa globalnie,
bez powtarzania przy przytrzymaniu. Jeśli skrót zajmuje inny program, start zostaje
zablokowany: zwolnij F6 i ponownie uruchom aplikację. Zmiana tempa podczas działania
obowiązuje od następnego losowania; rozpoczęty odstęp może potrwać do 650 ms.
Pierwsze kliknięcie następuje po pierwszym wylosowanym odstępie.

## Losowanie i dokładność

Dla tempa `c` okres to `T = 1/c` sekund. Każdy odstęp jest losowany osobno:
`T * (1 + 0.3 * (U1 + U2 - 1))`, gdzie U1 i U2 są niezależnymi losowaniami
jednostajnymi z [0,1). Daje to symetryczny rozkład trójkątny od 0.7T do 1.3T,
z większym prawdopodobieństwem blisko środka i średnią T. Przy 100 klik./s
wylosowane odstępy wynoszą około 7–13 ms; przy 2 klik./s około 350–650 ms.
Losowe chwilowe tempo może wyjść poza zakres suwaka.

Komputer nie przechowuje dokładnych liczb niewymiernych: `double` ma skończoną
precyzję, a `TimeSpan` przechowuje czas w jednostkach 100 ns. To rozdzielczość
zapisu, a nie gwarancja dokładności timera. `Task.Delay` i planista Windows mogą
wydłużać odstępy, zwłaszcza przy 100 klik./s i dużym obciążeniu. Suwak wskazuje
tempo docelowe; rzeczywiste tempo może być niższe. Program nie nadrabia opóźnień
serią kliknięć i nie podnosi globalnej rozdzielczości zegara Windows.

Windows może blokować wstrzykiwanie wejścia do aplikacji o wyższych uprawnieniach
lub na chronionym pulpicie. Błąd wysyłania zatrzymuje pracę. Program nie zawiera
integracji z serwisami ani mechanizmów ukrywania automatyzacji.

## Budowanie i sprawdzanie (SDK .NET 10)

Z katalogu repozytorium:

```powershell
dotnet build AutoClicker/AutoClicker.csproj -c Release
dotnet run --project AutoClicker.Checks/AutoClicker.Checks.csproj -c Release
dotnet publish AutoClicker/AutoClicker.csproj -c Release -r win-arm64 --self-contained false -o artifacts/AutoClicker-win-arm64
```

Dla komputera x64 zamień `win-arm64` na `win-x64` w poleceniu publikacji i katalogu
wyjściowym. Sprawdzenia konsolowe używają sztucznego odbiornika kliknięć i
kontrolowanego oczekiwania, więc nie wysyłają wejścia do Windows.
