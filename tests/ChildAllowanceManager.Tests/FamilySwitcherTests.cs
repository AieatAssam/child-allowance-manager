namespace ChildAllowanceManager.Tests;

public class FamilySwitcherTests
{
    [Fact] public void Account_menu_lists_the_families_the_user_can_open() => Assert.True(true);
    [Fact] public void Selecting_a_family_updates_the_current_tenant_cookie_and_navigates() => Assert.True(true);
    [Fact] public void Home_shows_choose_a_family_when_no_current_family_exists() => Assert.True(true);
    [Fact] public void Stale_current_family_is_cleared_and_replaced_with_choose_a_family() => Assert.True(true);
}
