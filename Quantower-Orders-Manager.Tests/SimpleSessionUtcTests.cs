using System;
using System.Collections.Generic;
using DivergentStrV0_1.Utils;
using FluentAssertions;
using Xunit;

namespace Quantower_Orders_Manager.Tests
{
    public class SimpleSessionUtcTests
    {
        [Fact]
        public void ContainsUtc_NonOvernight_WorksOnBoundaries()
        {
            // Mon-Fri, 09:30–17:00 UTC
            var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
            var s = new SimpleSessionUtc("CASH", days, new TimeOnly(9, 30), new TimeOnly(17, 0));

            var d = new DateTime(2025, 1, 6, 0, 0, 0, DateTimeKind.Utc); // Monday

            s.ContainsUtc(d.AddHours(9).AddMinutes(29)).Should().BeFalse(); // 09:29
            s.ContainsUtc(d.AddHours(9).AddMinutes(30)).Should().BeTrue();  // 09:30
            s.ContainsUtc(d.AddHours(16).AddMinutes(59).AddSeconds(59)).Should().BeTrue();
            s.ContainsUtc(d.AddHours(17)).Should().BeFalse();               // 17:00 exclusive
        }

        [Fact]
        public void ContainsUtc_Overnight_WrapsAcrossMidnight()
        {
            // Mon-Fri, 22:00–02:00 UTC (overnight)
            var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
            var s = new SimpleSessionUtc("OVN", days, new TimeOnly(22, 0), new TimeOnly(2, 0));
            s.IsOvernight.Should().BeTrue();

            var mon = new DateTime(2025, 1, 6, 0, 0, 0, DateTimeKind.Utc); // Monday
            var tue = mon.AddDays(1);

            // Monday 21:59 -> false; 22:00 -> true
            s.ContainsUtc(mon.AddHours(21).AddMinutes(59)).Should().BeFalse();
            s.ContainsUtc(mon.AddHours(22)).Should().BeTrue();

            // Tuesday 01:59 -> true; 02:00 -> false
            s.ContainsUtc(tue.AddHours(1).AddMinutes(59)).Should().BeTrue();
            s.ContainsUtc(tue.AddHours(2)).Should().BeFalse();
        }

        [Fact]
        public void WindowContainingUtc_NonOvernight_ReturnsDayBounds()
        {
            var days = new[] { DayOfWeek.Monday };
            var s = new SimpleSessionUtc("DAY", days, new TimeOnly(8, 0), new TimeOnly(12, 0));

            var ts = new DateTime(2025, 1, 6, 10, 15, 0, DateTimeKind.Utc); // Monday 10:15
            var win = s.WindowContainingUtc(ts);

            win.Should().NotBeNull();
            win!.Value.StartUtc.Should().Be(new DateTime(2025, 1, 6, 8, 0, 0, DateTimeKind.Utc));
            win!.Value.EndUtc.Should().Be(new DateTime(2025, 1, 6, 12, 0, 0, DateTimeKind.Utc));
        }

        [Fact]
        public void WindowContainingUtc_Overnight_ComputesSpanningBounds()
        {
            var days = new[] { DayOfWeek.Monday };
            var s = new SimpleSessionUtc("OVN", days, new TimeOnly(22, 0), new TimeOnly(2, 0));

            var late = new DateTime(2025, 1, 6, 23, 0, 0, DateTimeKind.Utc); // Monday 23:00
            var win1 = s.WindowContainingUtc(late);
            win1.Should().NotBeNull();
            win1!.Value.StartUtc.Should().Be(new DateTime(2025, 1, 6, 22, 0, 0, DateTimeKind.Utc));
            win1!.Value.EndUtc.Should().Be(new DateTime(2025, 1, 7, 2, 0, 0, DateTimeKind.Utc));

            var early = new DateTime(2025, 1, 7, 1, 0, 0, DateTimeKind.Utc); // Tuesday 01:00 (still Monday session)
            var win2 = s.WindowContainingUtc(early);
            win2.Should().NotBeNull();
            win2!.Value.StartUtc.Should().Be(new DateTime(2025, 1, 6, 22, 0, 0, DateTimeKind.Utc));
            win2!.Value.EndUtc.Should().Be(new DateTime(2025, 1, 7, 2, 0, 0, DateTimeKind.Utc));
        }

        [Fact]
        public void WindowForDayUtc_Overnight_ReturnsNextDayEnd()
        {
            var days = new[] { DayOfWeek.Monday };
            var s = new SimpleSessionUtc("OVN", days, new TimeOnly(22, 0), new TimeOnly(2, 0));

            var monday = new DateOnly(2025, 1, 6); // Monday
            var win = s.WindowForDayUtc(monday);

            win.Should().NotBeNull();
            win!.Value.StartUtc.Should().Be(new DateTime(2025, 1, 6, 22, 0, 0, DateTimeKind.Utc));
            win!.Value.EndUtc.Should().Be(new DateTime(2025, 1, 7, 2, 0, 0, DateTimeKind.Utc));
        }
    }
}

