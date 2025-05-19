param( $target = "raspberrypi", $user = "kenw")

rm output.txt
dotnet tool install --global dotnet-deb | write-output # this will only succeed the first time, do not show red warning as it is harmless.
dotnet deb -r linux-arm | tee output.txt
$match = select-string -path output.txt -Pattern "Created .+ package '([^']+)' .+";
if( -not $match.Matches.Success ) {
	throw "Build probably failed"
}
$deb = $match.Matches[0].Groups[1].Value;
$file = (Get-Item $deb).Name
do {
  scp "$deb" ${user}@${target}:/home/kenw/${file}
} while ($LASTEXITCODE -ne 0 )
ssh ${user}@${target} sudo dpkg -i ${file}