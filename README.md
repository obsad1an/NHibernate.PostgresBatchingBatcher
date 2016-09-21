# NHibernate.PostgresBatcher (Pre-alpha release. Use at own risk.)

After finding out that NHibernate doesn't have an implementation for [batching in Postgres](https://github.com/nhibernate/nhibernate-core/tree/master/src/NHibernate/AdoNet), I decided to make this library based on this old Stackoverflow [post](http://stackoverflow.com/questions/4611337/nhibernate-does-not-seems-doing-bulk-inserting-into-postgresql)

Credits to [Gerrit](http://stackoverflow.com/users/960796/gerrit) for the initial code.
The code needs a lot of refactoring which I intend to do but feel free to contribute.

## Usage
	
	config.DataBaseIntegration(db =>
		{
			db.BatchSize = 500; //this batch size is an example, set as needed
			db.Batcher<PostgresBatcherFactory>();
		});


## What does work:

	- Inserts batching
	- Updates batching (when the where statement only has one conditional)

## Work to do:

	- Clean the code
	- Update batching has to cover all the given SQL UPDATE Statement possibilities

## Updates

### 21/09/2016
	Update batching functionality added. Needs to be extended.
 
### 19/09/2016
	Initial commit. Code moved to a solution. Only insert batching worked.
