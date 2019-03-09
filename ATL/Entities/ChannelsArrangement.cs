namespace ATL
{
    public class ChannelsArrangements
    {
        public static ChannelsArrangement Mono = new ChannelsArrangement(1, "Mono (1/0)");
        public static ChannelsArrangement Stereo = new ChannelsArrangement(2, "Stereo (2/0)");

        public class ChannelsArrangement {
            private int nbChannels;
            private string description;

            public ChannelsArrangement(int nbChannels, string description)
            {
                this.nbChannels = nbChannels;
                this.description = description;
            }

            public string Description { get => description; set => description = value; }
            public int NbChannels { get => nbChannels; set => nbChannels = value; }
        }
    }
}
