﻿param( $target = "vertical-3", $user = "pi")
dotnet tool install --global dotnet-deb
dotnet deb -r linux-arm | tee output.txt
$match = select-string -path output.txt -Pattern ".+package '([^']+)' .+";
$deb = $match.Matches[1].Groups[1].value;
$file = (Get-Item $deb).Name
scp "$deb" ${user}@${target}:/home/${user}/${file}
ssh ${user}@${target} sudo dpkg -i ${file}