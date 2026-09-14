local Load = select(2, ...)
local DataToColor = Load[1]

function DataToColor:IsQuestActive(questId)
    local entryCount = GetNumQuestLogEntries()

    for index = 1, entryCount do
        local _, _, _, isHeader, _, _, _, currentQuestId = GetQuestLogTitle(index)

        if not isHeader and currentQuestId == questId then
            return true
        end
    end

    return false
end

function DataToColor:IsQuestReadyForTurnIn(questId)
    local entryCount = GetNumQuestLogEntries()

    for index = 1, entryCount do
        local _, _, _, isHeader, _, isComplete, _, currentQuestId = GetQuestLogTitle(index)

        if not isHeader and currentQuestId == questId then
            return isComplete == 1
        end
    end

    return false
end

function DataToColor:IsQuestCompleted(questId)
    return C_QuestLog.IsQuestFlaggedCompleted(questId) == true
end

function DataToColor:GetQuestState(questId)
    local state = 0

    if self:IsQuestActive(questId) then
        state = state + 1
    end

    if self:IsQuestReadyForTurnIn(questId) then
        state = state + 2
    end

    if self:IsQuestCompleted(questId) then
        state = state + 4
    end

    return state
end
