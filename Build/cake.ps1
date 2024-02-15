New-Cake -Name "ModelingEvolution.DirectConnect" -Root "../Source/ModelingEvolution.DirectConnect"

Add-CakeStep -Name "Build All" -Action {  Build-Dotnet -All  }
Add-CakeStep -Name "Publish to nuget.org" -Action { 
	Add-ApiToken "https://nuget.org" "oy2alzriq5357oabg7fq7axzjzhabnwufkljjqhkdyz3dm"
	Publish-Nuget -SourceUrl "https://nuget.org" 
}
