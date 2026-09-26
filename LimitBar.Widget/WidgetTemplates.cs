namespace LimitBar.Widget;

public static class WidgetTemplates
{
    public const string MainTemplate = """
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.5",
    "body": [
        {
            "type": "Container",
            "$data": "${providers}",
            "items": [
                {
                    "type": "TextBlock",
                    "text": "${providerName}",
                    "weight": "Bolder",
                    "size": "Medium",
                    "spacing": "Medium"
                },
                {
                    "type": "ColumnSet",
                    "columns": [
                        {
                            "type": "Column",
                            "width": "50px",
                            "items": [
                                {
                                    "type": "TextBlock",
                                    "text": "5h"
                                },
                                {
                                    "type": "TextBlock",
                                    "text": "Weekly"
                                }
                            ]
                        },
                        {
                            "type": "Column",
                            "width": "stretch",
                            "items": [
                                {
                                    "type": "TextBlock",
                                    "text": "${sessionText}",
                                    "horizontalAlignment": "Right"
                                },
                                {
                                    "type": "TextBlock",
                                    "text": "${weeklyText}",
                                    "horizontalAlignment": "Right"
                                }
                            ]
                        }
                    ]
                },
                {
                    "type": "TextBlock",
                    "text": "${resetText}",
                    "isSubtle": true,
                    "size": "Small",
                    "spacing": "None"
                }
            ]
        },
        {
            "type": "TextBlock",
            "text": "${lastUpdated}",
            "isSubtle": true,
            "size": "Small",
            "spacing": "Medium",
            "horizontalAlignment": "Center"
        }
    ]
}
""";
}
