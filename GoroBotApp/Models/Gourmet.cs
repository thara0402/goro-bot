using System;
using System.Collections.Generic;
using System.Text;

namespace GoroBotApp.Models
{
    public class Gourmet
    {
        public string Id { get; set; }

        public int Season { get; set; }

        public int Episode { get; set; }

        public string Title { get; set; }

        public string Restaurant { get; set; }

        public string Matome { get; set; }

        public string Access { get; set; }

        public string PhoneNumber { get; set; }

        public string Address { get; set; }

        public bool Closed { get; set; }

        public Location Location { get; set; }

    }

    public class Location
    {
        public float lat { get; set; }

        public float lng { get; set; }
    }

}
