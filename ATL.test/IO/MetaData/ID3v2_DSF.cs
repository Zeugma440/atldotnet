 using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Drawing.Imaging;

namespace ATL.test.IO.MetaData
{
    /*
     * IMPLEMENTED USE CASES
     *  
     *  1. Single metadata fields
     *                                Read  | Add   | Remove
     *  Supported textual field     |   x   |  x    | x
     *  Unsupported textual field   |   x   |  x    | x
     *  Supported picture           |   x   |  x    | x
     *  Unsupported picture         |   x   |  x    | x
     *  
     *  2. General behaviour
     *  
     *  Whole tag removal
     *  
     *  Conservation of unmodified tag items after tag editing
     *  Conservation of unsupported tag field after tag editing
     *  Conservation of supported pictures after tag editing
     *  Conservation of unsupported pictures after tag editing
     *  
     *  3. Specific behaviour
     *  
     *  Remove single supported picture (from normalized type and index)
     *  Remove single unsupported picture (with multiple pictures; checking if removing pic 2 correctly keeps pics 1 and 3)
     *
     */

    /*
     * TODO
     * 
     * FUNCTIONAL
     * 
     * Individual picture removal (from index > 1)
     * 
     * Extended ID3v2 header compliance cases incl. limit cases
     * 
     * 
     * TECHNICAL
     * 
     * Add a standard unsupported field => persisted as standard field in tag
     * Add a non-standard unsupported field => persisted as TXXX field
     * Exact picture data conservation after tag editing
     * 
     * Encode unsynchronized data
     * Decode unsynchronized data
     * 
    */


    [TestClass]
    public class ID3v2_DSF : MetaIOTest
    {
        public ID3v2_DSF()
        {
            emptyFile = "DSF/dsf.dsf";
            notEmptyFile = "DSF/dsf.dsf";
            tagType = MetaDataIOFactory.TAG_NATIVE;
        }

        [TestMethod]
        public void TagIO_R_DSF_ID3v2()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location));

            readExistingTagsOnFile(theFile);
        }
        
        [TestMethod]
        public void TagIO_RW_DSF_ID3v2_Existing()
        {
            test_RW_Existing(notEmptyFile, 2, true, true);
        }
    }
}
