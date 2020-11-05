print("EightOS")

local function inTable(tbl, el)
	for k, v in ipairs(tbl) do
		if v == el then
			return true
		end
	end
	return false
end

local n_screen = screen;

_G.screen = {}
function screen.setPixel(x, y, r, g, b)
	n_screen:SetPixel(x, y, r, g, b)
end
function screen.drawRectangle(x, y, w, h, r, g, b)
	n_screen:DrawRectangle(x, y, w, h, r, g, b)
end
function screen.setSize(w, h, s)
	n_screen:SetSize(w, h, s)
end
function screen.getSize()
	local sizes = n_screen:GetSize()
	return sizes[0], sizes[1], sizes[2]
end
function screen.setTickrate(t)
	n_screen:SetTickrate(t)
end
function screen.clear()
	n_screen:Clear()
end
function screen.setTitle(title)
	n_screen:SetTitle(title)
end
function screen.getTitle()
	return n_screen:GetTitle()
end

local n_os = os;
_G.os = {}
function os.timer(time)
	if time <= 0 then
		error("bad argument #1 (time must be greater than 0)", 2)
	end
	
	return n_os:Timer(time)
end

function os.sleep(ms)
	local timer = os.timer(ms or 1)
	
	local _, par
	repeat
		_, par = event.pull("timer")
	until par == timer
	return timer
end

function os.reset()
	n_os:Reset()
end

function os.quit()
	n_os:Quit()
end

_G.event = {}
local eventsQueue = {}
function event.pull(...)
	local filters = {...}
	if #filters > 0 then
		local ev = {}
		repeat
			ev = {coroutine.yield()}
		until inTable(filters, ev[1])
		return table.unpack(ev)
	else 
		return coroutine.yield()
	end
end
function event.push(...)
	eventsQueue[#eventsQueue+1] = {...}
end

local term = require("lua.term")

_G.term = term
term.init()

local cprint = print
_G.cprint = cprint

_G.print = term.print
_G.write = term.write

local func, err = loadfile("lua/init.lua")
if not func then
	error(err, 0)
end

local initThread = coroutine.create(func)

event.push("_eight_init")
local filter
local function resume()
	for i = 1, #eventsQueue do
		local event = eventsQueue[i]
		if filter == nil or filter == event[1] then
			local ok, par = coroutine.resume(initThread, table.unpack(event))
			if ok then
				filter = par
			else
				error(par, 0)
			end
		end
	end
	
	eventsQueue = {}
end


while true do
	local ev = {coroutine.yield()}
	
	if ev[1] == "_eight_tick" then
		eventsQueue[#eventsQueue+1] = {"tick"}
		resume()
	else
		eventsQueue[#eventsQueue+1] = ev
		resume()
	end
end