function Update()

	local targetString = SKIN:GetVariable('Target')

	local timeTarget = TimeStamp(targetString)
	local timeNow = os.time(os.date('*t'))

	local countDown = os.difftime(timeTarget, timeNow)

	if countDown < 0 then countDown = 0 end
	local weeks, days, hours, minutes, seconds = FormatSeconds(countDown)
	
	SKIN:Bang('!SetOption', 'MeasureHours', 'Formula', hours)
	SKIN:Bang('!SetOption', 'MeasureMinutes', 'Formula', minutes)
	SKIN:Bang('!SetOption', 'MeasureSeconds', 'Formula', seconds)
	
	return(countDown)

end

function TimeStamp(targetString)
   
   -- date format: 7/26/2017 8:32:10

   local month, day, year, hour, min, sec =
   string.match(targetString, "(%d+)/(%d+)/(%d+) (%d+):(%d+):(%d+)")

   local currentTimeTable = os.date()
   local currentDST = currentTimeTable['isdst']

   local timeStamp = os.time({year=year, month=month, day=day, hour=hour, min=min, sec=sec, isdst=currentDST})
   
   return timeStamp

end

function FormatSeconds(secondsArg)

	local weeks = math.floor(secondsArg / 604800)
	local remainder = secondsArg % 604800
	local days = math.floor(remainder / 86400)
	remainder = remainder % 86400
	local hours = math.floor(remainder / 3600)
	remainder = remainder % 3600
	local minutes = math.floor(remainder / 60)
	local seconds = remainder % 60
	
	return weeks, days, hours, minutes, seconds
	
end
