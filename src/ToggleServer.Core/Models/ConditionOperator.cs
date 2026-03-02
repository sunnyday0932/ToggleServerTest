namespace ToggleServer.Core.Models;

public enum ConditionOperator
{
    EQUALS,
    NOT_EQUALS,
    IN,
    NOT_IN,
    STARTS_WITH,
    ENDS_WITH,
    MATCHES_REGEX,
    PERCENTAGE_ROLLOUT // 針對 Hash 的數值做小於比對
}
