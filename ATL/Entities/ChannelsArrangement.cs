namespace ATL
{
    public class ChannelsArrangements
    {
        public static ChannelsArrangement Mono = new ChannelsArrangement(1, "Mono (1/0)");
        public static ChannelsArrangement DualMono = new ChannelsArrangement(2, "Dual Mono (1+1)");
        public static ChannelsArrangement Stereo = new ChannelsArrangement(2, "Stereo (2/0)");
        public static ChannelsArrangement StereoJoint = new ChannelsArrangement(2, "Joint Stereo");
        public static ChannelsArrangement StereoSumDifference = new ChannelsArrangement(2, "Stereo - Sum & Difference");
        public static ChannelsArrangement StereoLeftRightTotal = new ChannelsArrangement(2, "Stereo - Left & Right Total");
        public static ChannelsArrangement CLR = new ChannelsArrangement(3, "Center - Left - Right (3/0)");
        public static ChannelsArrangement LRS = new ChannelsArrangement(3, "Left - Right - Surround (3/1)");
        public static ChannelsArrangement LRSLSR = new ChannelsArrangement(4, "Left - Right - Surround Left - Surround Right (2/2)");
        public static ChannelsArrangement CLRSLSR = new ChannelsArrangement(5, "Center - Left - Right - Surround Left - Surround Right (3/2)");
        public static ChannelsArrangement CLCRLRSLSR = new ChannelsArrangement(6, "Center Left - Center Right - Left - Right - Surround Left - Surround Right");
        public static ChannelsArrangement CLRLRRRO = new ChannelsArrangement(6, "Center - Left - Right - Left Rear - Right Rear - Overhead");
        public static ChannelsArrangement CFCRLFRFLRRR = new ChannelsArrangement(6, "Center Front - Center Rear - Left Front - Right Front - Left Rear - Right Rear");
        public static ChannelsArrangement CLCCRLRSLSR = new ChannelsArrangement(7, "Center Left - Center - Center Right - Left - Right - Surround Left - Surround Right");
        public static ChannelsArrangement CLCRLRSL1SL2SR1SR2 = new ChannelsArrangement(8, "Center Left - Center Right - Left - Right - Surround Left 1 - Surround Left 2 - Surround Right 1 - Surround Right 2");
        public static ChannelsArrangement CLCCRLRSLSSR = new ChannelsArrangement(8, "Center Left - Center - Center Right - Left - Right - Surround Left - Surround - Surround Right");

        public class ChannelsArrangement {
            public ChannelsArrangement(int nbChannels, string description)
            {
                this.NbChannels = nbChannels;
                this.Description = description;
            }

            public string Description { get; set; }
            public int NbChannels { get; set; }
        }
    }
}
