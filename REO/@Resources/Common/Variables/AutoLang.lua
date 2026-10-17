--[[ AutoLang: 语言自动跟随系统。

     读注册表 HKCU\Control Panel\International 的 LocaleName（如 zh-CN），
     映射到八个语言包之一（zh→Chinese、ru→Russian、de→German、fr→French、
     es→Spanish、pt→Portuguese、nl→Dutch，其余回落 English）。
     在 LangAuto=1（默认）且 MainLanguage 与检测结果不同的时候，把两个语言键
     写成检测结果：
       MainLanguage -> #@#Common\Variables\UserVariables.inc   （磁贴/界面文案）
       Language     -> #@#Common\Background\Varrar.inc          （工具对话框）
     然后 !RefreshApp 让皮肤整体重载。值已一致时不写不刷，不会形成刷新循环。

     手动锁定：把 UserVariables.inc 的 LangAuto 改成 0，检测器即不再碰这两个
     文件；同时把 MainLanguage 与 Varrar.inc 的 Language 设成想要的语种
     （LangAuto=1 期间手改 MainLanguage 会在下次宿主加载时被系统语言覆盖）。

     挂载体：REO\Background\bg.ini 与 REO\Gallery\main.ini 末尾各挂了一对
     measure（Registry 读系统区域 + 本脚本），宿主每次加载时跑一次。
     MainLanguage=Auto 且检测器尚未落笔时，界面按英文渲染——
     Languages\Auto.inc 是指向 English.inc 的一行 shim（嵌套 include）。

     字符串字面量必须纯 ASCII（Rainmeter 按本机 ANSI/CP936 读 .lua）。
]]

local MAP = {
  zh = 'Chinese', ru = 'Russian', de = 'German', fr = 'French',
  es = 'Spanish', pt = 'Portuguese', nl = 'Dutch'
}

function Update()
  if SKIN:GetVariable('LangAuto') == '0' then return 0 end -- 手动锁定
  local current = SKIN:GetVariable('MainLanguage')
  if current == nil then return 0 end -- 宿主未引入 UserVariables.inc，无从判断
  local m = SKIN:GetMeasure('MeasureOmnimoAutoLangLocale')
  if m == nil then return 0 end
  local loc = m:GetStringValue() or ''
  local short = string.lower(string.sub(loc, 1, 2))
  local target = MAP[short] or 'English'
  if string.lower(current) == string.lower(target) then return 0 end
  local varFile = SELF:GetOption('VarFile', '')
  local toolFile = SELF:GetOption('ToolFile', '')
  if varFile ~= '' then
    SKIN:Bang('!WriteKeyValue', 'Variables', 'MainLanguage', target, varFile)
  end
  if toolFile ~= '' then
    SKIN:Bang('!WriteKeyValue', 'Variables', 'Language', target, toolFile)
  end
  SKIN:Bang('!RefreshApp')
  return 0
end
