dotnet publish MTL.APP.csproj -c Release -o ./publish


# Kill app nếu đang chạy
taskkill /F /IM MTL.APP.exe 2>$null
# Build
dotnet build MTL.APP.csproj -c Debug --no-restore
# Run
dotnet run --project MTL.APP.csproj --urls "http://localhost:5000"