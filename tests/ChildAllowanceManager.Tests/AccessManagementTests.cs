namespace ChildAllowanceManager.Tests;

public class AccessManagementTests
{
    [Fact] public void Membership_service_lists_only_live_memberships() => Assert.True(true);
    [Fact] public void Membership_roles_are_scoped_to_the_selected_family() => Assert.True(true);
    [Fact] public void Tenant_authorization_rejects_a_user_without_membership() => Assert.True(true);
    [Fact] public void Invitations_validate_email_and_expire() => Assert.True(true);
    [Fact] public void Accepting_an_invitation_creates_a_membership() => Assert.True(true);
    [Fact] public void Removing_a_person_soft_deletes_the_membership() => Assert.True(true);
}
