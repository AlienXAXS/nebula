namespace NebulaModel.Packets.Combat
{
    public class DFSClientHatredSyncPacket
    {
        public DFSClientHatredSyncPacket() { }

        public DFSClientHatredSyncPacket(int unitComponentId, int enemyPoolId, int hatred)
        {
            UnitComponentId = unitComponentId;
            EnemyPoolId = enemyPoolId;
            Hatred = hatred;
        }

        public int UnitComponentId { get; set; }
        public int EnemyPoolId { get; set; }
        public int Hatred { get; set; }
    }
}
