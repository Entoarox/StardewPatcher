cd "`dirname "$0"`"
COMMAND=""
if command -v command >/dev/null 2>&1; then
    COMMAND="command -v"
elif type type >/dev/null 2>&1; then
    COMMAND="type"
fi
if $COMMAND mono >/dev/null 2>&1; then
    mono StardewPatcher.exe
else
   echo "Oops! Looks like Mono isn't installed. Please install Mono from http://mono-project.com, reboot, and run this installer again."
   read
fi