local Load = select(2, ...)
local DataToColor = Load[1]

local GetAvailableQuests = C_GossipInfo.GetAvailableQuests
local GetActiveQuests = C_GossipInfo.GetActiveQuests
local GetQuestID = GetQuestID

local MAX_COUNT = 63
local MAX_QUEST_ID = 524280

local HEADER_BASE = 16777000
local HEADER_STRIDE = 64
local END_BASE = 16777192
local RECORD_STRIDE = 32
local STATE_STRIDE = 8

local OFFERED = 1
local ACTIVE = 2
local READY = 3
local OFFER_DETAIL = 4
local PROGRESS = 5
local REWARD = 6

local generation = -1

local function AddQuest(states, questId, state)
    if type(questId) ~= "number" or
        questId % 1 ~= 0 or
        questId <= 0 or
        questId > MAX_QUEST_ID then
        return
    end

    states[questId] = state
end

local function ToRecords(states)
    local records = {}

    for questId, state in pairs(states) do
        table.insert(records, {
            questId = questId,
            state = state
        })
    end

    table.sort(records, function(left, right)
        return left.questId < right.questId
    end)

    return records
end

function DataToColor:QueueQuestDialog(records)
    if #records > MAX_COUNT then
        return false
    end

    local queue = DataToColor.questDialogQueue

    if not queue then
        return false
    end

    generation = (generation + 1) % 3

    queue:clear()

    queue:push(HEADER_BASE + generation * HEADER_STRIDE + #records)

    for _, record in ipairs(records) do
        queue:push(
            record.questId * RECORD_STRIDE +
            generation * STATE_STRIDE +
            record.state)
    end

    queue:push(END_BASE + generation)

    return true
end

function DataToColor:QueueQuestGossip()
    local states = {}

    for _, quest in ipairs(GetAvailableQuests() or {}) do
        AddQuest(states, quest.questID, OFFERED)
    end

    for _, quest in ipairs(GetActiveQuests() or {}) do
        local state = quest.isComplete == true and READY or ACTIVE
        AddQuest(states, quest.questID, state)
    end

    return DataToColor:QueueQuestDialog(ToRecords(states))
end

function DataToColor:QueueQuestPage(state)
    local states = {}

    AddQuest(states, GetQuestID(), state)

    return DataToColor:QueueQuestDialog(ToRecords(states))
end

function DataToColor:OnQuestDetail()
    DataToColor:QueueQuestPage(OFFER_DETAIL)
end

function DataToColor:OnQuestProgress()
    DataToColor:QueueQuestPage(PROGRESS)
end

function DataToColor:OnQuestComplete()
    DataToColor:QueueQuestPage(REWARD)
end

function DataToColor:OnQuestFinished()
    DataToColor:QueueQuestDialog({})
end

function DataToColor:OnGossipClosed()
    DataToColor:QueueQuestDialog({})
end
