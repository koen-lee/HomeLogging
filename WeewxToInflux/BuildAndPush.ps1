param( $target = "raspberrypi", $user = "kenw")
dotnet tool install --global dotnet-deb
dotnet deb -r linux-arm | tee output.txt
$match = select-string -path output.txt -Pattern ".+package '([^']+)' .+";
$deb = $match.Matches[1].Groups[1].value;
Remove-Item output.txt
$file = (Get-Item $deb).Name
scp "$deb" ${user}@${target}:/home/${user}/${file}
ssh ${user}@${target} sudo dpkg -i ${file}