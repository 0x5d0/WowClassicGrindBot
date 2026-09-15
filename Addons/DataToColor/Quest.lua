local Load = select(2, ...)
local DataToColor = Load[1]

local QUEST_HEADER_BASE = 16777000
local QUEST_HEADER_GENERATION_STRIDE = 64
local QUEST_END_BASE = 16777192
local QUEST_RECORD_STRIDE = 16
local QUEST_STATUS_STRIDE = 4

local QUEST_STATUS_INCOMPLETE = 1
local QUEST_STATUS_READY_FOR_TURN_IN = 2
local QUEST_STATUS_FAILED = 3

local MAX_GENERATION = 2
local MAX_SNAPSHOT_COUNT = 63
local MAX_QUEST_ID = 1048561
local QUEST_SNAPSHOT_REFRESH_SECONDS = 15

local questSnapshotGeneration = -1
local questSnapshotDirty = true
local questSnapshotNextRefreshTime = 0

function DataToColor:IsQuestActive(questId)
    local entryCount = GetNumQuestLogEntries()

    for index = 1, entryCount do
        local _, _, _, isHeader, _, _, _, currentQuestId =
            GetQuestLogTitle(index)

        if not isHeader and currentQuestId == questId then
            return true
        end
    end

    return false
end

function DataToColor:IsQuestReadyForTurnIn(questId)
    local entryCount = GetNumQuestLogEntries()

    for index = 1, entryCount do
        local _, _, _, isHeader, _, isComplete, _, currentQuestId =
            GetQuestLogTitle(index)

        if not isHeader and currentQuestId == questId then
            return isComplete == 1
        end
    end

    return false
end

function DataToColor:IsQuestCompleted(questId)
    return C_QuestLog.IsQuestFlaggedCompleted(questId) == true
end

function DataToColor:MarkQuestSnapshotDirty()
    questSnapshotDirty = true
end

function DataToColor:OnQuestLogUpdate()
    DataToColor:MarkQuestSnapshotDirty()
end

function DataToColor:UpdateQuestSnapshot()
    local now = GetTime()

    if now >= questSnapshotNextRefreshTime then
        questSnapshotDirty = true
    end

    if not questSnapshotDirty then
        return
    end

    local queue = DataToColor.questQueue

    if not queue then
        return
    end

    if queue:peek() ~= nil then
        return
    end

    DataToColor:QueueQuestSnapshot(now)
end

function DataToColor:QueueQuestSnapshot(now)
    local records = {}
    local seenQuestIds = {}
    local entryCount = GetNumQuestLogEntries()

    for index = 1, entryCount do
        local _, _, _, isHeader, _, isComplete, _, questId =
            GetQuestLogTitle(index)

        if not isHeader and
            type(questId) == "number" and
            questId > 0 and
            questId <= MAX_QUEST_ID and
            not seenQuestIds[questId] then

            local status = QUEST_STATUS_INCOMPLETE

            if isComplete == 1 then
                status = QUEST_STATUS_READY_FOR_TURN_IN
            elseif isComplete == -1 then
                status = QUEST_STATUS_FAILED
            end

            seenQuestIds[questId] = true

            table.insert(records,
                {
                    questId = questId,
                    status = status
                })
        end
    end

    if #records > MAX_SNAPSHOT_COUNT then
        DataToColor:Print(
            "Quest snapshot ignored: too many active quests (" ..
            #records .. ")")

        questSnapshotDirty = false

        questSnapshotNextRefreshTime =
            (now or GetTime()) + QUEST_SNAPSHOT_REFRESH_SECONDS
        return
    end

    table.sort(records, function(left, right)
        return left.questId < right.questId
    end)

    questSnapshotGeneration =
        (questSnapshotGeneration + 1) % (MAX_GENERATION + 1)

    local queue = DataToColor.questQueue

    queue:push(
        QUEST_HEADER_BASE +
        questSnapshotGeneration * QUEST_HEADER_GENERATION_STRIDE +
        #records)

    for _, record in ipairs(records) do
        queue:push(
            record.questId * QUEST_RECORD_STRIDE +
            questSnapshotGeneration * QUEST_STATUS_STRIDE +
            record.status)
    end

    queue:push(QUEST_END_BASE + questSnapshotGeneration)

    questSnapshotDirty = false

    questSnapshotNextRefreshTime =
        (now or GetTime()) + QUEST_SNAPSHOT_REFRESH_SECONDS
end
