local Load = select(2, ...)
local DataToColor = Load[1]

local GetNumQuestLogEntries = GetNumQuestLogEntries
local GetQuestLogTitle = GetQuestLogTitle
local ExpandQuestHeader = ExpandQuestHeader
local CollapseQuestHeader = CollapseQuestHeader
local GetTime = GetTime
local IsQuestFlaggedCompleted = C_QuestLog.IsQuestFlaggedCompleted

local QUEST_HEADER_BASE = 16777000
local QUEST_HEADER_GENERATION_STRIDE = 64
local QUEST_END_BASE = 16777192
local QUEST_RECORD_STRIDE = 16
local QUEST_STATUS_STRIDE = 4
local QUEST_HISTORY_RECORD_STRIDE = 8
local QUEST_HISTORY_STATUS_STRIDE = 2
local MAX_HISTORY_QUEST_ID = 2097124

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
local questHistoryGeneration = -1
local questHistoryDirty = false
local questHistoryRefreshAt = 0

local function ExpandCollapsedQuestHeaders()
    local collapsedHeaderTitles = {}
    local index = 1
    local entryCount = GetNumQuestLogEntries()

    while index <= entryCount do
        local title, _, _, isHeader, isCollapsed =
            GetQuestLogTitle(index)

        if isHeader and isCollapsed then
            table.insert(collapsedHeaderTitles, title)
            ExpandQuestHeader(index)

            -- Expanding a header can reveal more entries and headers.
            entryCount = GetNumQuestLogEntries()
        end

        index = index + 1
    end

    return collapsedHeaderTitles
end

local function RestoreCollapsedQuestHeaders(collapsedHeaderTitles)
    -- Restore from the innermost/last expanded header first.
    for i = #collapsedHeaderTitles, 1, -1 do
        local wantedTitle = collapsedHeaderTitles[i]
        local entryCount = GetNumQuestLogEntries()

        for index = 1, entryCount do
            local title, _, _, isHeader, isCollapsed =
                GetQuestLogTitle(index)

            if isHeader and
                not isCollapsed and
                title == wantedTitle then

                CollapseQuestHeader(index)
                break
            end
        end
    end
end

local function CollectActiveQuestRecords()
    local collapsedHeaderTitles = {}

    local success, recordsOrError = pcall(function()
        collapsedHeaderTitles = ExpandCollapsedQuestHeaders()

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

        return records
    end)

    RestoreCollapsedQuestHeaders(collapsedHeaderTitles)

    if not success then
        DataToColor:Print(
            "Quest snapshot failed: " .. tostring(recordsOrError))

        return nil
    end

    return recordsOrError
end

function DataToColor:IsQuestActive(questId)
    local records = CollectActiveQuestRecords()

    if not records then
        return false
    end

    for _, record in ipairs(records) do
        if record.questId == questId then
            return true
        end
    end

    return false
end

function DataToColor:IsQuestReadyForTurnIn(questId)
    local records = CollectActiveQuestRecords()

    if not records then
        return false
    end

    for _, record in ipairs(records) do
        if record.questId == questId then
            return record.status == QUEST_STATUS_READY_FOR_TURN_IN
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
    DataToColor:MarkQuestHistoryDirty()
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

    -- Finish the active snapshot before queuing its replacement.
    if queue:peek() ~= nil then
        return
    end

    DataToColor:QueueQuestSnapshot(now)
end

function DataToColor:QueueQuestSnapshot(now)
    local records = CollectActiveQuestRecords()

    if not records then
        questSnapshotDirty = false
        questSnapshotNextRefreshTime =
            (now or GetTime()) + QUEST_SNAPSHOT_REFRESH_SECONDS

        return
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

local function NormalizeQuestHistoryIds(ids)
    if type(ids) ~= "table" then
        return nil
    end

    local result = {}
    local seen = {}

    for _, id in ipairs(ids) do
        if type(id) ~= "number" or
            id % 1 ~= 0 or
            id <= 0 or
            id > MAX_HISTORY_QUEST_ID then

            return nil
        end

        if not seen[id] then
            seen[id] = true
            table.insert(result, id)

            if #result > MAX_SNAPSHOT_COUNT then
                return nil
            end
        end
    end

    table.sort(result)

    return result
end

local savedQuestHistoryIds =
    NormalizeQuestHistoryIds(DataToColorQuestHistory)

local questHistoryIds = savedQuestHistoryIds or {}
local questHistoryRequested = savedQuestHistoryIds ~= nil

function DataToColor:SetQuestHistory(ids)
    local nextIds = NormalizeQuestHistoryIds(ids)

    if not nextIds then
        return false
    end

    questHistoryIds = nextIds
    DataToColorQuestHistory = nextIds
    questHistoryRequested = true

    if DataToColor.questHistoryQueue then
        DataToColor.questHistoryQueue:clear()
    end

    DataToColor:MarkQuestHistoryDirty()

    return true
end

function DataToColor:RestoreQuestHistory()
    local ids = NormalizeQuestHistoryIds(DataToColorQuestHistory)

    if ids then
        DataToColor:SetQuestHistory(ids)
    end
end

function DataToColor:MarkQuestHistoryDirty()
    if questHistoryRequested then
        questHistoryDirty = true
    end
end

local function ReadQuestHistory(ids, isCompleted)
    local records = {}

    for index, questId in ipairs(ids) do
        local ok, completed =
            pcall(isCompleted, questId)

        if not ok then
            return nil
        end

        records[index] = completed == true
    end

    return records
end

function DataToColor:UpdateQuestHistory()
    if not questHistoryRequested then
        return
    end

    local now = GetTime()

    if now >= questHistoryRefreshAt then
        questHistoryDirty = true
    end

    if not questHistoryDirty then
        return
    end

    local queue = DataToColor.questHistoryQueue

    if not queue or queue:peek() ~= nil then
        return
    end

    DataToColor:QueueQuestHistory(now)
end

function DataToColor:QueueQuestHistory(now)
    local records = ReadQuestHistory(questHistoryIds, IsQuestFlaggedCompleted)

    if not records then
        questHistoryDirty = false
        questHistoryRefreshAt =
            (now or GetTime()) + QUEST_SNAPSHOT_REFRESH_SECONDS

        return
    end

    questHistoryGeneration =
        (questHistoryGeneration + 1) % (MAX_GENERATION + 1)

    local queue = DataToColor.questHistoryQueue

    queue:push(
        QUEST_HEADER_BASE +
        questHistoryGeneration * QUEST_HEADER_GENERATION_STRIDE +
        #questHistoryIds)

    for index, questId in ipairs(questHistoryIds) do
        local completed = records[index] and 1 or 0

        queue:push(
            questId * QUEST_HISTORY_RECORD_STRIDE +
            questHistoryGeneration * QUEST_HISTORY_STATUS_STRIDE +
            completed)
    end

    queue:push(QUEST_END_BASE + questHistoryGeneration)

    questHistoryDirty = false
    questHistoryRefreshAt =
        (now or GetTime()) + QUEST_SNAPSHOT_REFRESH_SECONDS
end
