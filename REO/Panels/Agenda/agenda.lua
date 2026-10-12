--[[ Agenda: 解析 iCalendar（ICS）订阅源，按"今天起 RangeDays 天"排成可滚动列表。

     数据通路：每路订阅一个 WebParser（整页捕获）-> GetStringValue()
     -> 本脚本合并去重、按时间排序、按日期分组，并驱动行表。

     场景：只支持公开 ICS 订阅（webcal:// 会转成 https://）。
     iCloud 私密 CalDAV 端点需要 PROPFIND，Rainmeter 的 WebParser 只能 GET，做不了。

     时区：DTSTART 支持 Z（UTC）、TZID 参数、无参数的本地时间。TZID 用下面的固定偏移表
     换算到本机时间；表里没有的时区按本机时间原样显示，并在日志里提示一次。
     固定偏移不跟夏令时走（欧洲/北美部分区域在夏令时期间可能差一小时），这是刻意取舍：
     一个日历面板不值得内置 tzdata。日期运算走整数日历、不经过 os.time，所以 1970 年
      之前的时刻不会把刷新打断：表示不了的日期整条跳过。

     重复事件：支持 RRULE 的有界子集（DAILY/WEEKLY/MONTHLY/YEARLY + INTERVAL/COUNT/
     UNTIL/BYDAY/BYMONTHDAY/BYMONTH），只展开面板窗口内的实例。带其它参数（WKST/BYSETPOS/
     BYHOUR…）或不支持的组合时，规则整条不认，退回"只画 DTSTART 那一条"——宁可少画也不画错。
     RDATE/EXDATE/RECURRENCE-ID 属性一律忽略。

     语言：提示文案走语言包（AgendaLoading/AgendaUnavailable/AgendaNoFeed/AgendaNoEvents，
     未翻译的语言自动回落英文）。AgendaNoEvents 里的 %1 会替换成天数——这是本仓库语言文件
     里唯一的占位符约定（其它键都是纯文本）。星期缩写与 "all day" 保持英文不翻译。

     引号：SUMMARY 里带双引号时值用三引号形式送出；连续三个以上引号会被 Rainmeter 的
      参数解析器截断（残余还会被当成皮肤名去执行），这类标题折叠成两个引号显示。

      性能：面板为了"静置回顶"设了 Update=1000（见 Item.ini），但每秒的 tick 会在订阅正文、
     日期窗口、滚动偏移都没变时直接返回，既不再解析 ICS 也不重推上百个 !SetOption（Update 里
     的 parseKey/renderKey 判定）。
]]

local DAYNAMES = { 'Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat' }

-- 死源判定阈值（秒）：WebParser 默认 30s 超时，超过此值仍无有效 ICS 即判源不可用
local LOAD_TIMEOUT = 45

-- RRULE 展开的迭代上限：正常订阅远达不到，纯粹防止畸形规则把 Rainmeter 拖住
local MAX_ITER = 20000

-- ICS 星期缩写 -> os.date('*t').wday（1=周日 .. 7=周六）
local WDAY = { SU = 1, MO = 2, TU = 3, WE = 4, TH = 5, FR = 6, SA = 7 }

-- TZID -> 相对 UTC 的小时数。固定偏移（不随夏令时切换），覆盖常见公开订阅源。
local TZ_OFFSET = {
  ['UTC'] = 0, ['Etc/UTC'] = 0, ['GMT'] = 0,
  ['Europe/London'] = 0, ['Europe/Dublin'] = 0, ['Europe/Lisbon'] = 0,
  ['Europe/Paris'] = 1, ['Europe/Berlin'] = 1, ['Europe/Madrid'] = 1, ['Europe/Rome'] = 1,
  ['Europe/Amsterdam'] = 1, ['Europe/Brussels'] = 1, ['Europe/Vienna'] = 1, ['Europe/Prague'] = 1,
  ['Europe/Warsaw'] = 1, ['Europe/Stockholm'] = 1, ['Europe/Zurich'] = 1, ['Europe/Budapest'] = 1,
  ['Europe/Athens'] = 2, ['Europe/Helsinki'] = 2, ['Europe/Kyiv'] = 2, ['Europe/Kiev'] = 2,
  ['Europe/Bucharest'] = 2, ['Europe/Istanbul'] = 3, ['Europe/Moscow'] = 3,
  ['Asia/Jerusalem'] = 2, ['Asia/Dubai'] = 4, ['Asia/Karachi'] = 5,
  ['Asia/Kolkata'] = 5.5, ['Asia/Calcutta'] = 5.5, ['Asia/Dhaka'] = 6,
  ['Asia/Bangkok'] = 7, ['Asia/Jakarta'] = 7,
  ['Asia/Shanghai'] = 8, ['Asia/Chongqing'] = 8, ['Asia/Hong_Kong'] = 8, ['Asia/Taipei'] = 8,
  ['Asia/Singapore'] = 8, ['Asia/Kuala_Lumpur'] = 8, ['Asia/Manila'] = 8, ['Asia/Ulaanbaatar'] = 8,
  ['Asia/Tokyo'] = 9, ['Asia/Seoul'] = 9,
  ['Australia/Perth'] = 8, ['Australia/Brisbane'] = 10, ['Australia/Sydney'] = 10,
  ['Australia/Melbourne'] = 10, ['Pacific/Auckland'] = 12,
  ['America/Sao_Paulo'] = -3, ['America/Argentina/Buenos_Aires'] = -3,
  ['America/New_York'] = -5, ['America/Toronto'] = -5, ['America/Chicago'] = -6,
  ['America/Mexico_City'] = -6, ['America/Denver'] = -7, ['America/Los_Angeles'] = -8,
  ['America/Vancouver'] = -8,
  -- Windows 时区名：部分客户端（Outlook 等）导出时用这套名字
  ['China Standard Time'] = 8, ['Tokyo Standard Time'] = 9, ['Korea Standard Time'] = 9,
  ['India Standard Time'] = 5.5, ['GMT Standard Time'] = 0, ['W. Europe Standard Time'] = 1,
  ['Eastern Standard Time'] = -5, ['Central Standard Time'] = -6, ['Mountain Standard Time'] = -7,
  ['Pacific Standard Time'] = -8, ['AUS Eastern Standard Time'] = 10,
  ['New Zealand Standard Time'] = 12,
}

local feeds, VIS, headerH, eventH, viewH, listTop, rowW, padX, tc, rangeDays, hasData
local lastOff, lastMove, loadStart, warnedTzid, lastMaxOff, warnedQuote, warnedParse
local cache = { parseKey = nil, renderKey = nil, msgKey = nil, rows = {}, count = 0 }

local function num(v, d)
  local n = tonumber(v)
  if n == nil then return d end
  return n
end

local function unescape(s)
  if not s then return '' end
  s = s:gsub('\\n', ' '):gsub('\\N', ' ')
  s = s:gsub('\\,', ','):gsub('\\;', ';'):gsub('\\\\', '\\')
  return s:gsub('^%s+', ''):gsub('%s+$', '')
end

-- 儒略日序数（1970-01-01 = 0），纯整数运算（Hinnant 的 days_from_civil）。os.time 对
-- 1970 之前的墙上时间返回 nil（Windows 的 C 运行库表示不了），而周对齐会退到上一周、
-- 时区换算也可能退过 epoch，所以日期算术一律不走 os.time。
local function dayNum(y, m, d)
  y = y - ((m <= 2) and 1 or 0)
  local era = math.floor(y / 400)
  local yoe = y - era * 400
  local doy = math.floor((153 * (m + ((m > 2) and -3 or 9)) + 2) / 5) + d - 1
  local doe = yoe * 365 + math.floor(yoe / 4) - math.floor(yoe / 100) + doy
  return era * 146097 + doe - 719468
end

-- dayNum 的逆运算
local function dayToYMD(z)
  z = z + 719468
  local era = math.floor(z / 146097)
  local doe = z - era * 146097
  local yoe = math.floor((doe - math.floor(doe / 1460) + math.floor(doe / 36524)
    - math.floor(doe / 146096)) / 365)
  local y = yoe + era * 400
  local doy = doe - (365 * yoe + math.floor(yoe / 4) - math.floor(yoe / 100))
  local mp = math.floor((5 * doy + 2) / 153)
  local d = doy - math.floor((153 * mp + 2) / 5) + 1
  local m = mp + (mp < 10 and 3 or -9)
  if m <= 2 then y = y + 1 end
  return y, m, d
end

-- 本机相对 UTC 的偏移（秒）。按给定时刻取，跨夏令时比固定值准。
local function localOffset(e)
  if not e then return nil end
  return os.difftime(os.time(os.date('*t', e)), os.time(os.date('!*t', e)))
end

-- 把"某时区的墙上时间"换成本机墙上时间；offHours = 该时区相对 UTC 的小时数。
-- 源时刻或换算结果落到 os.time 表示不了的范围时返回 nil（调用方按"跳过该事件"处理）。
local function toLocal(y, m, d, H, M, offHours)
  local e = os.time({ year = y, month = m, day = d, hour = H, min = M, sec = 0 })
  local off = localOffset(e)
  if not off then return nil end
  local lt = os.date('*t', e + off - offHours * 3600)
  return { y = lt.year, m = lt.month, d = lt.day, H = lt.hour, M = lt.min }
end

-- 只收 os.time 表示得了的日期。表示不了的（1970 之前）整条事件丢弃，而不是等到第一次
-- 取时间戳时抛 "attempt to perform arithmetic on a nil value" 把整个刷新打断。
local function dayFields(y, m, d, H, M)
  if not os.time({ year = y, month = m, day = d, hour = 12 }) then return nil end
  return { y = y, m = m, d = d, H = H, M = M }
end

-- params = 属性名后面的参数串（形如 ";TZID=Asia/Shanghai"）；v = 值
local function parseDT(params, v)
  if not v then return nil end
  v = v:gsub('^[^:]*:', '')                -- 兼容传入整行（含属性名与参数）
  local tzid = params and params:match('TZID=([^;:]*)')
  if tzid then tzid = tzid:gsub('"', '') end
  local isUTC = v:sub(-1) == 'Z'
  v = v:gsub('Z$', '')
  local y, m, d, H, M = v:match('^(%d%d%d%d)(%d%d)(%d%d)T(%d%d)(%d%d)')
  if y then
    y, m, d, H, M = num(y), num(m), num(d), num(H), num(M)
    if isUTC then
      -- 源以 UTC(Z) 给出时刻；os.time 把入参当本地时间，需补回本地与 UTC 的偏移再读本地分量
      return toLocal(y, m, d, H, M, 0)
    end
    if tzid then
      local off = TZ_OFFSET[tzid]
      if off then return toLocal(y, m, d, H, M, off) end
      if not warnedTzid then
        warnedTzid = true
        -- keep this string pure ASCII: Rainmeter reads .lua as ANSI, so non-ASCII literals come out mangled
        SKIN:Bang('!Log "Agenda: unknown TZID=' .. tzid .. ', showing the event at this machine local time" Notice')
      end
    end
    return dayFields(y, m, d, H, M)
  end
  local y2, m2, d2 = v:match('^(%d%d%d%d)(%d%d)(%d%d)')
  if y2 then return dayFields(num(y2), num(m2), num(d2)) end
  return nil
end

local function dayKey(t) return t.y * 10000 + t.m * 100 + t.d end
local function sortKey(t) return dayKey(t) * 10000 + num(t.H, 0) * 100 + num(t.M, 0) end
local function hhmm(t) return string.format('%02d:%02d', num(t.H, 0), num(t.M, 0)) end

-- 取 ICS 属性：[1] = 参数串（可能为空），[2] = 值
local function propRaw(blk, name)
  return blk:match('\n' .. name .. '([^:\r\n]*):([^\r\n]*)')
end

local function prop(blk, name)
  local _, v = propRaw(blk, name)
  return v
end

local function daysBetween(y1, m1, d1, y2, m2, d2)
  return dayNum(y2, m2, d2) - dayNum(y1, m1, d1)
end

local function addDays(y, m, d, n)
  return dayToYMD(dayNum(y, m, d) + n)
end

-- 1=周日 .. 7=周六，与 os.date('*t').wday 对齐（1970-01-01 是周四 = 5）
local function wdayOf(y, m, d) return (dayNum(y, m, d) + 4) % 7 + 1 end

local function daysInMonth(y, m)
  if m == 2 then
    return (y % 4 == 0 and (y % 100 ~= 0 or y % 400 == 0)) and 29 or 28
  end
  return (m == 4 or m == 6 or m == 9 or m == 11) and 30 or 31
end

-- 给重复实例加上与首个实例相同的时长；全天事件（没有 H 字段）保持"没有时刻"的形状
local function addSecs(t, secs)
  local base = os.time({ year = t.y, month = t.m, day = t.d, hour = num(t.H, 0), min = num(t.M, 0), sec = 0 })
  if not base then return nil end
  local lt = os.date('*t', base + secs)
  local o = { y = lt.year, m = lt.month, d = lt.day }
  if t.H then o.H, o.M = lt.hour, lt.min end
  return o
end

local function parseBYDAY(v)
  local out = {}
  for tok in v:gmatch('[^,]+') do
    local ord, wd = tok:match('^([%+%-]?%d*)(%a%a)$')
    if not wd then return nil end
    local n = WDAY[wd:upper()]
    if not n then return nil end
    local o = nil
    if ord ~= '' then
      o = tonumber(ord)
      if o == nil or o == 0 then return nil end
    end
    out[#out + 1] = { ord = o, wd = n }
  end
  if #out == 0 then return nil end
  return out
end

local function parseNumList(v, lo, hi)
  local out = {}
  for tok in v:gmatch('[^,]+') do
    local n = tonumber(tok)
    if n == nil or n == 0 or n < lo or n > hi then return nil end
    out[#out + 1] = n
  end
  if #out == 0 then return nil end
  return out
end

local FREQ_OK = { DAILY = true, WEEKLY = true, MONTHLY = true, YEARLY = true }

-- 解析 RRULE 的支持子集；不支持的规则返回 nil（调用方只画 DTSTART 那一条）
local function parseRRule(v)
  if not v or v == '' then return nil end
  local r = { freq = nil, interval = 1, count = nil, until_ = nil,
              byday = nil, bymonthday = nil, bymonth = nil }
  for part in v:gmatch('[^;]+') do
    local k, val = part:match('^([^=]+)=(.*)$')
    if not k then return nil end
    if k == 'FREQ' then
      r.freq = val:upper()
    elseif k == 'INTERVAL' then
      r.interval = tonumber(val)
      if not r.interval or r.interval < 1 then return nil end
    elseif k == 'COUNT' then
      r.count = tonumber(val)
      if not r.count or r.count < 1 then return nil end
    elseif k == 'UNTIL' then
      r.until_ = parseDT(nil, val)
      if not r.until_ then return nil end
    elseif k == 'BYDAY' then
      r.byday = parseBYDAY(val)
      if not r.byday then return nil end
    elseif k == 'BYMONTHDAY' then
      r.bymonthday = parseNumList(val, -31, 31)
      if not r.bymonthday then return nil end
    elseif k == 'BYMONTH' then
      r.bymonth = parseNumList(val, 1, 12)
      if not r.bymonth then return nil end
      table.sort(r.bymonth)
    else
      return nil                            -- 不认识的参数（WKST/BYSETPOS/BYHOUR…）一律不猜
    end
  end
  if not r.freq or not FREQ_OK[r.freq] then return nil end
  -- 频率与参数的组合：只认语义明确的，其余整条退回 DTSTART
  if r.freq == 'DAILY' and (r.byday or r.bymonth or r.bymonthday) then return nil end
  if r.freq == 'WEEKLY' then
    if r.bymonth or r.bymonthday then return nil end
    if r.byday then                          -- WEEKLY 的 BYDAY 带序数（2TU）没有意义
      for _, b in ipairs(r.byday) do
        if b.ord then return nil end
      end
    end
  end
  if (r.freq == 'MONTHLY' or r.freq == 'YEARLY') and r.byday and r.bymonthday then return nil end
  if r.freq == 'MONTHLY' and r.bymonth then return nil end   -- BYMONTH 对 MONTHLY 是 limit，容易错
  return r
end

-- 某个月里符合 BYMONTHDAY / BYDAY 的日期列表（两者都没有就用 defDay）
local function monthDays(y, m, bymd, bydays, defDay)
  local n = daysInMonth(y, m)
  local out, seen = {}, {}
  local function push(d)
    if d >= 1 and d <= n and not seen[d] then
      seen[d] = true
      out[#out + 1] = d
    end
  end
  if bymd then
    for _, v in ipairs(bymd) do push(v > 0 and v or (n + v + 1)) end
  elseif bydays then
    for _, b in ipairs(bydays) do
      if not b.ord then
        for d = 1, n do
          if wdayOf(y, m, d) == b.wd then push(d) end
        end
      elseif b.ord > 0 then
        local c = 0
        for d = 1, n do
          if wdayOf(y, m, d) == b.wd then
            c = c + 1
            if c == b.ord then push(d); break end
          end
        end
      else
        local c = 0
        for d = n, 1, -1 do
          if wdayOf(y, m, d) == b.wd then
            c = c - 1
            if c == b.ord then push(d); break end
          end
        end
      end
    end
  else
    push(defDay)
  end
  table.sort(out)
  return out
end

-- 把重复事件在 [lo, hi] 窗口内展开，追加到 out（首个实例由调用方自己放）
local function expandRRule(ev, lo, hi, out)
  local r = ev.rrule
  local s = ev.s
  local loK, hiK = dayKey(lo), dayKey(hi)
  local iter, count = 0, 0
  local untilK = r.until_ and dayKey(r.until_) or nil

  local function mk(y, m, d)
    local t = { y = y, m = m, d = d }
    if s.H then t.H, t.M = s.H, s.M end
    return t
  end

  -- 返回 false = 这条规则到此为止（COUNT / UNTIL 用尽）
  local function emit(t)
    local k = dayKey(t)
    if untilK and k > untilK then return false end
    count = count + 1
    if r.count and count > r.count then return false end
    if k >= loK and k <= hiK then
      local inst = { s = t, title = ev.title, key = sortKey(t) }
      if ev.dur then inst.e = addSecs(t, ev.dur) end
      out[#out + 1] = inst
    end
    return true
  end

  local function tick()
    iter = iter + 1
    if iter > MAX_ITER then
      SKIN:Bang('!Log "Agenda: RRULE expansion hit the ' .. MAX_ITER .. ' step cap, truncated" Notice')
      return false
    end
    return true
  end

  if r.freq == 'DAILY' then
    local y, m, d = s.y, s.m, s.d
    if not r.count then
      -- 没有 COUNT 时可以直接跳到窗口左端，不必从很远的 DTSTART 一天天数过来
      local skip = math.max(0, math.ceil(daysBetween(y, m, d, lo.y, lo.m, lo.d) / r.interval))
      y, m, d = addDays(y, m, d, skip * r.interval)
    end
    while tick() do
      if not emit(mk(y, m, d)) then return end
      if y * 10000 + m * 100 + d > hiK then return end
      y, m, d = addDays(y, m, d, r.interval)
    end

  elseif r.freq == 'WEEKLY' then
    -- 以周一为一周之始（RFC 默认 WKST=MO），从 DTSTART 所在周起每 interval 周展开一周
    local set = {}
    if r.byday then
      for _, b in ipairs(r.byday) do set[b.wd] = true end
    else
      set[wdayOf(s.y, s.m, s.d)] = true
    end
    local back = (wdayOf(s.y, s.m, s.d) + 5) % 7          -- 周一 -> 0，周日 -> 6
    local y, m, d = addDays(s.y, s.m, s.d, -back)
    if not r.count then
      local ly, lm, ld = addDays(lo.y, lo.m, lo.d, -((wdayOf(lo.y, lo.m, lo.d) + 5) % 7))
      local weeks = math.max(0, math.ceil(daysBetween(y, m, d, ly, lm, ld) / (7 * r.interval)))
      y, m, d = addDays(y, m, d, weeks * 7 * r.interval)
    end
    local k0 = dayKey(s)
    while tick() do
      for i = 0, 6 do
        local y2, m2, d2 = addDays(y, m, d, i)
        if set[wdayOf(y2, m2, d2)] and y2 * 10000 + m2 * 100 + d2 >= k0 then
          if not emit(mk(y2, m2, d2)) then return end
        end
      end
      if y * 10000 + m * 100 + d > hiK then return end
      y, m, d = addDays(y, m, d, 7 * r.interval)
    end

  elseif r.freq == 'MONTHLY' then
    local y, m = s.y, s.m
    local k0 = dayKey(s)
    if not r.count then
      local skip = math.max(0, math.ceil(((lo.y * 12 + lo.m) - (y * 12 + m)) / r.interval))
      for _ = 1, skip do m = m + r.interval end
      while m > 12 do m = m - 12; y = y + 1 end
    end
    while tick() do
      for _, d in ipairs(monthDays(y, m, r.bymonthday, r.byday, s.d)) do
        if y * 10000 + m * 100 + d >= k0 then
          if not emit(mk(y, m, d)) then return end
        end
      end
      if y * 100 + m > math.floor(hiK / 100) then return end
      m = m + r.interval
      while m > 12 do m = m - 12; y = y + 1 end
    end

  elseif r.freq == 'YEARLY' then
    local k0 = dayKey(s)
    local months = r.bymonth or { s.m }
    local y = s.y
    if not r.count then
      y = s.y + math.max(0, math.ceil((lo.y - s.y) / r.interval)) * r.interval
    end
    while tick() do
      for _, m in ipairs(months) do
        for _, d in ipairs(monthDays(y, m, r.bymonthday, r.byday, s.d)) do
          if y * 10000 + m * 100 + d >= k0 then
            if not emit(mk(y, m, d)) then return end
          end
        end
      end
      if y > math.floor(hiK / 10000) then return end
      y = y + r.interval
    end
  end
end

-- 解析一段 ICS，把窗口内的实例追加到 out
local function parseICS(raw, lo, hi, out)
  raw = raw:gsub('\r\n[ \t]', ''):gsub('\n[ \t]', '')
  for blk in raw:gmatch('BEGIN:VEVENT(.-)END:VEVENT') do
    local dp, dv = propRaw(blk, 'DTSTART')
    local s = parseDT(dp, dv)
    if s then
      local ep, ev = propRaw(blk, 'DTEND')
      local e = parseDT(ep, ev)
      local title = unescape(prop(blk, 'SUMMARY'))
      if title == '' then title = '(untitled)' end
      local evt = { s = s, e = e, title = title, key = sortKey(s) }
      if e then
        local sd = os.time({ year = s.y, month = s.m, day = s.d, hour = num(s.H, 0), min = num(s.M, 0), sec = 0 })
        local ed = os.time({ year = e.y, month = e.m, day = e.d, hour = num(e.H, 0), min = num(e.M, 0), sec = 0 })
        if sd and ed and ed > sd then evt.dur = ed - sd end
      end
      out[#out + 1] = evt
      local rrule = parseRRule(prop(blk, 'RRULE'))
      if rrule then
        evt.rrule = rrule
        expandRRule(evt, lo, hi, out)
      end
    end
  end
end

local function buildRows(events)
  local rows, curDay = {}, nil
  for _, ev in ipairs(events) do
    local k = dayKey(ev.s)
    local dayText = string.format('%s %d', DAYNAMES[wdayOf(ev.s.y, ev.s.m, ev.s.d)], ev.s.d)

    local r = { kind = 'ev', h = eventH, title = ev.title }
    if ev.s.H and ev.e and (ev.e.H or ev.e.M) then
      r.time = hhmm(ev.s) .. '-' .. hhmm(ev.e)
    elseif ev.s.H then
      r.time = hhmm(ev.s)
    else
      r.time = 'all day'
    end

    if k ~= curDay then
      curDay = k
      rows[#rows + 1] = { kind = 'day', h = headerH, text = dayText }
    end
    rows[#rows + 1] = r
  end
  return rows
end

local function totalHeight(rows)
  local t = 0
  for _, r in ipairs(rows) do t = t + r.h end
  return t
end

local function hideAll()
  for i = 1, VIS do
    SKIN:Bang('!SetOption Row' .. i .. ' Hidden 1')
    SKIN:Bang('!SetOption Bar' .. i .. ' Hidden 1')
    SKIN:Bang('!SetOption Time' .. i .. ' Hidden 1')
  end
end

-- 值里带双引号时用三引号形式（Rainmeter 只剥最外层三引号，里面的引号原样保留）；
-- 否则沿用普通写法。（源码见 CommandHandler::ParseString 的三引号分支）
-- 例外：连续三个及以上引号会被那个解析器当成收尾符，值被腰斩、残余还会被当作皮肤名
-- 执行（"!SetOption: Skin "quote" does not exist"）。这种标题折叠成两个引号照常显示，
-- 只丢一点标点，好过整行静默消失。
local function set(meter, opt, val)
  val = tostring(val)
  if val:find('"""', 1, true) then
    if not warnedQuote then
      warnedQuote = true
      SKIN:Bang('!Log "Agenda: a title has a run of quotes, collapsed to two so the row can still render" Notice')
    end
    val = val:gsub('"""+', '""')
  end
  if val:find('"', 1, true) then
    SKIN:Bang('!SetOption ' .. meter .. ' ' .. opt .. ' """' .. val .. '"""')
  else
    SKIN:Bang('!SetOption ' .. meter .. ' ' .. opt .. ' "' .. val .. '"')
  end
end

local function render(slot, r, y)
  -- 左留白随面板宽度走（窄档位若沿用固定 30px，右侧只剩 10px，视觉重心会偏右）
  local x = '(' .. padX .. ')*#ScaleDpi#'
  -- 文字必须让开竖条：原先两者同 X，文字直接压在竖条上
  local textX = '(' .. (padX + 10) .. ')*#ScaleDpi#'
  local w = '(' .. rowW .. ')*#ScaleDpi#'

  if r.kind == 'day' then
    set('Row' .. slot, 'Text', r.text)
    set('Row' .. slot, 'FontColor', tc .. ',235')
    set('Row' .. slot, 'FontSize', '(#Height#/13)*#ScaleDpi#')
    set('Row' .. slot, 'StringAlign', 'LeftTop')
    set('Row' .. slot, 'X', x)
    set('Row' .. slot, 'W', w)
    set('Row' .. slot, 'Y', y + math.floor(headerH * 0.24))
    set('Row' .. slot, 'Hidden', 0)
    -- 日期标题行不显示时间：同一槽位上一帧可能是事件行，残留的时间文案要清掉
    set('Time' .. slot, 'Text', '')
    return
  end

  local inner = math.floor(eventH * 0.06)
  local lineGap = math.floor(eventH * 0.48)

  set('Bar' .. slot, 'SolidColor', tc .. ',90')
  set('Bar' .. slot, 'X', x)
  set('Bar' .. slot, 'Y', y + inner)
  set('Bar' .. slot, 'W', '(#Height#/70)*#ScaleDpi#')
  set('Bar' .. slot, 'H', '(' .. (eventH - 2 * inner) .. ')*#ScaleDpi#')
  set('Bar' .. slot, 'Hidden', 0)
  set('Row' .. slot, 'Text', r.title)
  set('Row' .. slot, 'FontColor', tc .. ',255')
  set('Row' .. slot, 'FontSize', '(#Height#/14)*#ScaleDpi#')
  set('Row' .. slot, 'StringAlign', 'LeftTop')
  set('Row' .. slot, 'X', textX)
  set('Row' .. slot, 'W', w)
  set('Row' .. slot, 'Y', y + inner + 1)
  set('Row' .. slot, 'Hidden', 0)
  set('Time' .. slot, 'Text', r.time)
  set('Time' .. slot, 'FontColor', tc .. ',125')
  set('Time' .. slot, 'FontSize', '(#Height#/17)*#ScaleDpi#')
  set('Time' .. slot, 'StringAlign', 'LeftTop')
  set('Time' .. slot, 'X', textX)
  set('Time' .. slot, 'W', w)
  set('Time' .. slot, 'Y', y + inner + 1 + lineGap)
  set('Time' .. slot, 'Hidden', 0)
end

local function layout(rows, off)
  hideAll()
  local slot, top = 0, 0
  for _, r in ipairs(rows) do
    if top + r.h > off then
      if slot >= VIS then break end
      if top - off < viewH then
        slot = slot + 1
        render(slot, r, listTop + (top - off))
      end
    end
    top = top + r.h
  end
  SKIN:Bang('!UpdateMeterGroup Rows')
  SKIN:Bang('!Redraw')
end

-- 便宜的正文指纹：长度 + 首尾片段 + 采样字节和。逐字节哈希每秒跑一遍不划算，
-- 采样足以发现订阅内容变化（WebParser 只在 UpdateRate 到期时才换正文）。
local function fingerprint(s)
  if not s or s == '' then return '0' end
  local sum, n, i = 0, #s, 1
  while i <= n do
    sum = sum + s:byte(i)
    i = i + 97
  end
  return n .. ':' .. s:sub(1, 48) .. ':' .. s:sub(-48) .. ':' .. (sum % 65536)
end

local function msgText(key, fallback)
  return SKIN:GetVariable(key, fallback)
end

function Initialize()
  feeds = {}
  local n = num(SKIN:GetVariable('FeedCount'), 1)
  for i = 1, n do
    local url = SKIN:GetVariable('Feed' .. i, '')
    if url ~= '' then
      -- iCloud 给出的是 webcal:// 形式，WebParser 只认 http(s)
      local https = url:gsub('^webcal://', 'https://')
      if https ~= url then
        set('MeasureICS' .. i, 'Url', https)
      end
      -- 测量默认禁用，地址就位后再启用：避免加载瞬间用 webcal:// 去抓而报 12006
      SKIN:Bang('!EnableMeasure MeasureICS' .. i)
      SKIN:Bang('!UpdateMeasure MeasureICS' .. i)
      local ok, m = pcall(function() return SKIN:GetMeasure('MeasureICS' .. i) end)
      if ok and m then feeds[#feeds + 1] = m end
    end
  end
  VIS = num(SKIN:GetVariable('VisibleRows'), 6)
  headerH = num(SKIN:GetVariable('HeaderH'), 24)
  eventH = num(SKIN:GetVariable('EventH'), 40)
  viewH = num(SKIN:GetVariable('ViewH'), 260)
  listTop = num(SKIN:GetVariable('ListTop'), 22)
  rowW = num(SKIN:GetVariable('RowW'), 250)
  padX = num(SKIN:GetVariable('PadX'), 30)
  tc = SKIN:GetVariable('textcolor2', '255,255,255')
  rangeDays = num(SKIN:GetVariable('RangeDays'), 6)
  lastOff, lastMove = 0, os.time()
  lastMaxOff = nil
  cache.parseKey, cache.renderKey, cache.msgKey = nil, nil, nil
  cache.rows, cache.count = {}, 0
end

function Update()
  -- 1) 收正文：只读字符串与算指纹，不解析（解析放到第 4 步的缓存判定之后）
  local fps, fetched = {}, false
  for i, m in ipairs(feeds) do
    local raw = m:GetStringValue()
    fps[i] = fingerprint(raw)
    if raw and raw:find('BEGIN:VCALENDAR', 1, true) then fetched = true end
  end
  local feedKey = table.concat(fps, ';')

  -- 2) 滚动偏移与静置回顶（与订阅内容无关，每秒都算，成本只有几次比较）
  local offVar = num(SKIN:GetVariable('Offset'), 0)
  local off = offVar
  if off ~= lastOff then lastOff, lastMove = off, os.time() end
  if off > 0 and os.time() - lastMove > 6 then off = 0 end
  if off < 0 then off = 0 end

  local today = os.date('*t')
  local loK = today.year * 10000 + today.month * 100 + today.day

  -- 3) 还没拿到任何正文：只维护提示文案，正文一到就自己恢复
  if not fetched then
    if #feeds == 0 then
      local key = 'msg|nofeed|' .. feedKey
      if key ~= cache.msgKey then
        cache.msgKey = key
        set('Msg', 'Text', msgText('AgendaNoFeed', 'no feed configured'))
        set('Msg', 'Hidden', 0)
        SKIN:Bang('!UpdateMeter Msg')
      end
      return 'no feeds'
    end
    -- 取到过数据时源暂时为空，不该把已显示的内容顶上这行字，也不该重置计时
    if hasData then return 'loading' end
    local now = os.time()
    if loadStart == nil then loadStart = now end
    -- 区分「还在加载」与「源已死」：超过 LOAD_TIMEOUT 仍无有效 ICS 则报不可用
    local state = (now - loadStart >= LOAD_TIMEOUT) and 'unavailable' or 'loading'
    local key = 'msg|' .. state .. '|' .. feedKey
    if key ~= cache.msgKey then
      cache.msgKey = key
      if state == 'unavailable' then
        set('Msg', 'Text', msgText('AgendaUnavailable', 'feed unavailable'))
      else
        set('Msg', 'Text', msgText('AgendaLoading', 'loading feed...'))
      end
      set('Msg', 'Hidden', 0)
      SKIN:Bang('!UpdateMeter Msg')
    end
    return 'loading'
  end
  loadStart = nil

  -- 4) 解析 / 排序 / 分组：只在订阅正文或日期窗口变化时做。
  --    面板 Update=1000 是为了"静置回顶"，正文与窗口都没变时这一步整体跳过。
  local lo = { y = today.year, m = today.month, d = today.day }
  -- RangeDays 就是"今天起显示多少天"，右端用 (rangeDays-1) 个日历日，
  -- 才和设置面板的标签"显示天数（今天起）"对得上（按日历日推进，不受夏令时影响）。
  local hy, hm, hd = addDays(lo.y, lo.m, lo.d, math.max(0, rangeDays - 1))
  local hi = { y = hy, m = hm, d = hd }
  local hiK = hy * 10000 + hm * 100 + hd

  local parseKey = feedKey .. '|' .. loK .. '|' .. hiK
  if parseKey ~= cache.parseKey then
    local ok = pcall(function()
      local all, seen = {}, {}
      for _, m in ipairs(feeds) do
        local raw = m:GetStringValue()
        if raw and raw:find('BEGIN:VCALENDAR', 1, true) then
          local got = {}
          parseICS(raw, lo, hi, got)
          for _, ev in ipairs(got) do
            local sig = dayKey(ev.s) .. '|' .. num(ev.s.H, 0) .. ':' .. num(ev.s.M, 0) .. '|' .. ev.title
            if not seen[sig] then
              seen[sig] = true
              all[#all + 1] = ev
            end
          end
        end
      end
      -- 每个订阅内部 parseICS 已排好，但跨源合并后仍是"一路接一路"，必须重排
      table.sort(all, function(a, b)
        if a.key ~= b.key then return a.key < b.key end
        return a.title < b.title
      end)

      local inRange = {}
      for _, ev in ipairs(all) do
        local k = dayKey(ev.s)
        if k >= loK and k <= hiK then inRange[#inRange + 1] = ev end
      end
      if #inRange > 0 then hasData = true end
      cache.rows = buildRows(inRange)
      cache.count = #inRange
    end)
    -- 解析成功才提交 parseKey：失败时保留上一份行表，下一个 tick 还会重试
    -- （旧写法先提交再解析，一次坏数据会让面板卡到订阅正文变化为止）。
    if ok then
      cache.parseKey = parseKey
      warnedParse = false
    elseif not warnedParse then
      warnedParse = true
      SKIN:Bang('!Log "Agenda: could not parse a feed, keeping the last good list" Error')
    end
  end
  local rows = cache.rows

  local mx = math.max(0, totalHeight(rows) - viewH)
  if off > mx then off = mx end
  -- 与变量里的原值比较：被夹到上限或静置回顶后要把新值写回变量，
  -- 否则下次滚动会以过期的偏移为基准。
  if off ~= offVar then
    SKIN:Bang('!SetVariable Offset ' .. off)
  end
  if mx ~= lastMaxOff then
    lastMaxOff = mx
    SKIN:Bang('!SetVariable MaxOffset ' .. mx)
  end

  -- 5) 渲染：偏移或行表真的变了才推 bangs
  local empty = (#rows == 0)
  local renderKey = parseKey .. '|' .. off .. '|' .. tostring(empty)
  if renderKey ~= cache.renderKey then
    cache.renderKey = renderKey
    if empty then
      -- 天数按 RangeDays 直接填；未翻译的语言回落英文默认值
      local text = msgText('AgendaNoEvents', 'no events in the next %1 days')
      set('Msg', 'Text', (text:gsub('%%1', tostring(rangeDays))))
      set('Msg', 'Hidden', 0)
    else
      set('Msg', 'Hidden', 1)
    end
    SKIN:Bang('!UpdateMeter Msg')
    layout(rows, off)
  end
  return tostring(cache.count)
end
