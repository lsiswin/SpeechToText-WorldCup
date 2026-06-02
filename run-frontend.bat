@echo off
title SpeechToText Vue Frontend
echo Installing frontend dependencies (if not present)...
cd frontend
if not exist node_modules (
    call npm install
)
echo.
echo Starting Vue Development Server...
call npm run dev
pause
