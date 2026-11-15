namespace Room0317.Chapter2
{
    /// <summary>
    /// Linear story progression steps for Chapter 2: The Cold Corridor.
    /// Enforces sequential evidence discovery and prevents early ending triggers.
    /// </summary>
    public enum Chapter2Step
    {
        NotStarted = 0,
        EnterCorridor = 1,
        InspectExits = 2,
        ReceiveGhostSms = 3,
        FollowFootprints = 4,
        InspectUmbrella = 5,
        InvestigateIntercom = 6,
        DiscoverCompactMirror = 7,
        ReachRoofDoor = 8,
        InspectFleeceJacket = 9,
        ReachMovingBox = 10,
        FinalRevelation = 11,
        Completed = 12
    }
}
