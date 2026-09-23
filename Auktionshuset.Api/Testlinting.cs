namespace Auktionshuset.Api
{
    public class Testlinting
    {

        private int counter = 0;

        public void TestExplicitTypes()
        {
            // Skal gerne markeres: I foretrækker explicit type
            var number = 10;
            var text = "Hello World";
            var date = DateTime.Now;
            var numbers = new List<int>();

            // Disse bør være OK
            int explicitNumber = 20;
            string explicitText = "Hello";
            DateTime explicitDate = DateTime.Now;
            List<int> explicitNumbers = new List<int>();
        }

        public void TestBraces(bool isActive)
        {
            // Skal gerne markeres: mangler braces
            if (isActive)
                Console.WriteLine("Active");

            // Skal være OK
            if (isActive)
            {
                Console.WriteLine("Active");
            }
        }

        public int TestNormalReturn()
        {
            // Roslyn bør foreslå expression-bodied method
            return 42;
        }

        public string GetName()
        {
            // Roslyn bør foreslå expression-bodied method
            return "Megalodon";
        }

        public int Counter
        {
            // Roslyn bør foreslå expression-bodied property
            get
            {
                return counter;
            }
        }

        public void TestUnusedAssignment()
        {
            int number = 10;

            // Første værdi bliver overskrevet uden at blive brugt
            number = 20;

            Console.WriteLine(number);
        }

        public void TestObjectInitializer()
        {
            // Bør foreslå object initializer
            TestPerson person = new TestPerson();
            person.Name = "Bob";
            person.Age = 42;

            Console.WriteLine(person.Name);
        }

        public void TestCollectionInitializer()
        {
            // Bør foreslå collection initializer
            List<string> names = new List<string>();

            names.Add("Bob");
            names.Add("Alice");
            names.Add("Charlie");

            Console.WriteLine(names.Count);
        }


        public class TestPerson
        {
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }
    }
}

