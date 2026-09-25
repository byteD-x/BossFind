using BossFind.Domain.Enums;
using Microsoft.UI.Xaml.Data;
using System;

namespace BossFind.App;

public sealed class FactCategoryDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            CandidateFactCategory.Education => "教育经历",
            CandidateFactCategory.Experience => "工作经历",
            CandidateFactCategory.Project => "项目经历",
            CandidateFactCategory.Skill => "技能",
            CandidateFactCategory.Certificate => "证书",
            CandidateFactCategory.Achievement => "成就",
            CandidateFactCategory.Preference => "求职偏好",
            _ => value?.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
