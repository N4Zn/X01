@echo off
rem Chay: chay.bat            -> bao cao cho HOM NAY
rem       chay.bat 2026-10-09 -> bao cao cho ngay chi dinh
rem Tai log tu Google Sheet (link trong config.json), cham diem, ra Excel + PDF trong ketqua\<ngay>\
chcp 65001 >nul
cd /d "%~dp0"
set D=%1
if "%D%"=="" for /f %%i in ('python -c "import datetime;print(datetime.date.today())"') do set D=%%i
python -X utf8 run.py --from %D% --to %D% --out ketqua\%D%
if errorlevel 1 (echo LOI - xem thong bao o tren & pause & exit /b 1)
explorer ketqua\%D%
pause
