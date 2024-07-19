@echo off

:: remove the cloned repository to ensure a clean start
rd /s /q GradientSoftware.GithubScripts

:: Get the current branch name
for /f %%i in ('git rev-parse --abbrev-ref HEAD') do set "branch=%%i"
echo Current branch: %branch%

:: Clone the repository containing the script
git clone --single-branch --branch main https://github.com/%GITHUB_USERNAME%/GradientSoftware.GithubScripts.git

:: Run the script from the cloned repository
dotnet script ./GradientSoftware.GithubScripts/.github/scripts/update-version.csx %GITHUB_USERNAME% GradientSoftware.REPONAME %GITHUB_APIKEY% .\REPONAME\REPONAME.csproj %branch%

:: remove the cloned repository
rd /s /q GradientSoftware.GithubScripts
