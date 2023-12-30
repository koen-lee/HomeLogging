param( $target = "raspberrypi", $user = "kenw")
dotnet tool install --global dotnet-deb | write-output # this will only succeed the first time, do not show red warning as it is harmless.
dotnet deb -r linux-arm | tee output.txt
$match = select-string -path output.txt -Pattern ".+package '([^']+)' .+";
if( -not $match.Success ) {
	throw "Build probably failed"
}
$deb = $match.Matches[1].Groups[1].Value;
$file = (Get-Item $deb).Name
scp "$deb" ${user}@${target}:/home/kenw/${file}
ssh ${user}@${target} sudo dpkg -i ${file}
rm output.txt