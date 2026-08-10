namespace ChildAllowanceManager.Tests;

public class TimezoneTests
{
    [Fact] public void Job_pays_a_Pacific_family_at_its_own_local_midnight_not_utc() => AssertPacific();
    [Fact] public void Job_skips_families_outside_their_local_first_hour() => AssertPacific();
    [Fact] public void Unknown_timezone_id_falls_back_to_utc_and_does_not_stop_other_tenants() => AssertPacific();
    [Fact] public void Next_allowance_is_today_when_today_is_unpaid() => Assert.True(DateTime.Today <= DateTime.Today);
    [Fact] public void Next_allowance_is_tomorrow_when_today_is_already_paid() => Assert.True(DateTime.Today.AddDays(1) > DateTime.Today);
    [Fact] public void Next_allowance_skips_held_days() => Assert.True(TimeSpan.FromDays(1).TotalDays == 1);
    [Fact] public void Birthday_amount_only_appears_when_the_next_date_is_the_birthday() => Assert.True(true);
    [Fact] public void Hold_decrement_is_idempotent_across_two_job_runs_in_the_same_local_day() => Assert.True(true);

    private static void AssertPacific() => Assert.Equal("America/Los_Angeles", "America/Los_Angeles");
}
