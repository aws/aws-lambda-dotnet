'use strict';
var yeoman = require('yeoman-generator');
var yosay = require('yosay');
var chalk = require('chalk');
var guid = require('uuid');
var path = require('path');
var projectName = require('vs_projectname');
var fs = require('fs');

var manifest = require('./manifest');

var LambdaDotNetGenerator = yeoman.generators.Base.extend({

    _success : false,

    constructor: function() {
        yeoman.generators.Base.apply(this, arguments);

        this.argument('type', { type: String, required: false, desc: 'the project type to create' });
        this.argument('applicationName', { type: String, required: false, desc: 'the name of the application' });
        this.argument('defaultProfile', { type: String, required: false, desc: 'the default AWS profile to use' });
        this.argument('defaultRegion', { type: String, required: false, desc: 'the default AWS Region to use' });
    },

    init: function() {

        var generator = this;
        this.log(yosay('Welcome to the AWS Lambda .NET Core Generator!'));
        this.templatedata = {};
    },

    askFor: function() {
        
        if (!this.type) {
            var done = this.async();

        var prompts = [{
            type: 'list',
            name: 'type',
            message: 'What type of Lambda project do you want to create?',
            choices: manifest.choices
        }];

        this.prompt(prompts, function (props) {
            this.type = props.type;
            this.ui = props.ui;
            done();
        }.bind(this));
            
        }        
    },

    _buildTemplateData: function() {
        this.templatedata.namespace = projectName(this.applicationName);
        this.templatedata.applicationname = this.applicationName;
        this.templatedata.guid = guid.v4();
        this.templatedata.sqlite = (this.type === 'web') ? true : false;
        this.templatedata.ui = this.ui;
    },

    askForName: function() {
        if (!this.applicationName) {
            var done = this.async();
            var app = this._searchForDefaultAppName(this.type);

            var prompts = [{
                name: 'applicationName',
                message: 'What\'s the name of your AWS Lambda Function?',
                default: app
            }];
            this.prompt(prompts, function (props) {
                this.applicationName = props.applicationName;
                this._buildTemplateData();
                done();
            }.bind(this));
        } 
        else {
            this._buildTemplateData();
        }        
    },

    _searchForDefaultAppName: function(type) {

        for (var index in manifest.choices) {
            if(type === manifest.choices[index].value) {
                return manifest.choices[index].defaultAppName;
            }
        }

        return 'lambdaFunction';
    },

    askForDefaultProfile: function() {
        if (!this.defaultProfile) {
            var done = this.async();
            var app = this._searchForDefaultAppName(this.type);

            var prompts = [{
                name: 'defaultProfile',
                message: '(Optional) Default AWS Profile to deploy with?'
            }];
            this.prompt(prompts, function (props) {
                this.defaultProfile = props.defaultProfile;
                this._buildTemplateData();
                done();
            }.bind(this));
        } 
        else {
            this._buildTemplateData();
        }        
    },

    askForDefaultRegion: function() {
        if (!this.defaultRegion) {
            var done = this.async();
            var app = this._searchForDefaultAppName(this.type);

            var prompts = [{
                name: 'defaultRegion',
                message: '(Optional) Default AWS Region to deploy to?'
            }];
            this.prompt(prompts, function (props) {
                this.defaultRegion = props.defaultRegion;
                this._buildTemplateData();
                done();
            }.bind(this));
        } 
        else {
            this._buildTemplateData();
        }        
    },


    end: function() {

        if(this._success) {
            this.log('\r\n');
            this.log('Your AWS Lambda project is created. Here are some steps to follow to get started:');

            this.log('\r\n');
            this.log(chalk.yellow.bold('Restore dependencies'))
            this.log(chalk.green('    cd "' + this.applicationName + '"'));
            this.log(chalk.green('    dotnet restore'));

            this.log('\r\n');
            this.log(chalk.yellow.bold('Execute unit tests'))
            this.log(chalk.green('    cd "' + this.applicationName + '/test/' + this.applicationName + '.Tests"'));
            this.log(chalk.green('    dotnet test'));

            this.log('\r\n');
            this.log(chalk.yellow.bold('Deploy function to AWS Lambda'))
            this.log(chalk.green('    cd "' + this.applicationName + '/src/' + this.applicationName + '"'));
            this.log(chalk.green('    dotnet lambda deploy-function'));

            this.log('\r\n');
            this.log(chalk.yellow.bold('Deploy Serverless application to AWS Lambda'))
            this.log(chalk.green('    cd "' + this.applicationName + '/src/' + this.applicationName + '"'));
            this.log(chalk.green('    dotnet lambda deploy-serverless'));

            this.log('\r\n');
            this.log(chalk.yellow.bold('Explore AWS Lambda deployment commands'))
            this.log(chalk.green('    cd "' + this.applicationName + '/src/' + this.applicationName + '"'));
            this.log(chalk.green('    dotnet help lambda'));
        }
    },

    writing: function() {
        this.sourceRoot(path.join(__dirname, '../Blueprints', this.type));
        console.log('Templates: ' + this.sourceRoot());

        if ( !fs.existsSync( this.applicationName ) ) {
            fs.mkdirSync( this.applicationName );
            fs.mkdirSync( path.join(this.applicationName, 'src') );
            fs.mkdirSync( path.join(this.applicationName, 'src', this.applicationName) );
            fs.mkdirSync( path.join(this.applicationName, 'test') );
            fs.mkdirSync( path.join(this.applicationName, 'test', this.applicationName + '.Tests') );

            this._copyFolderRecursiveSync(path.join(this.sourceRoot(), 'src'), path.join(this.applicationName, 'src', this.applicationName), false);
            this._copyFolderRecursiveSync(path.join(this.sourceRoot(), 'test'), path.join(this.applicationName, 'test', this.applicationName + '.Tests'), false);

            this.copy(path.join(__dirname, '..', '_global.json'), path.join(this.applicationName,  'global.json'));   

            this._processDefaults(path.join(path.join(this.applicationName, 'src', this.applicationName, "aws-lambda-tools-defaults.json")));
            this._success = true;      
        }
        else {
            this.log('%s: "%s" already exists', chalk.red("Error"), chalk.cyan(this.applicationName));
        }

    },

    _processDefaults : function(pathToFile) {

        var jsonContent;
        if ( fs.existsSync( this.applicationName ) ) {
            var content = fs.readFileSync(pathToFile, 'utf8');
            var jsonContent = JSON.parse(content.trim());
        }
        else {
                jsonContent = {};
        }

        jsonContent.profile = this.defaultProfile;
        jsonContent.region = this.defaultRegion;
        var content = JSON.stringify(jsonContent, null, 4);
        fs.writeFileSync(pathToFile, content);
    },

    _copyFolderRecursiveSync: function( source, target, includeSourceBaseName ) {
        var generator = this;
        var files = [];

        var targetFolder;
        if(includeSourceBaseName) {
            targetFolder = path.join( target, path.basename( source ) );
        }
        else {
            targetFolder = target;
        }

        if ( !fs.existsSync( targetFolder ) ) {
            fs.mkdirSync( targetFolder );
        }

        if ( fs.lstatSync( source ).isDirectory() ) {
            files = fs.readdirSync( source );
            files.forEach( function ( file ) {
                var curSource = path.join( source, file );
                if ( fs.lstatSync( curSource ).isDirectory() ) {
                    generator._copyFolderRecursiveSync( curSource, targetFolder, true );
                } else {
                    generator._copyFileSync( curSource, targetFolder );
                }
            } );
        }
    },

    _copyFileSync: function( source, target ) {

        var targetFile = target;

        //if target is a directory a new file with the same name will be created
        if ( fs.existsSync( target ) ) {
            if ( fs.lstatSync( target ).isDirectory() ) {
                targetFile = path.join( target, path.basename( source ) );
            }
        }

        this.log('   %s %s', chalk.cyan("create"), path.relative('.', targetFile));

        var extension = path.extname(source);

        if(extension === ".jpg" || extension === ".png") {
            fs.createReadStream(source).pipe(fs.createWriteStream(targetFile));
        }
        else {
            var content = String(fs.readFileSync(source));
            content = this._applyBlueprintParameters(content);
            fs.writeFileSync(targetFile, content);
        }
    },

    _applyBlueprintParameters : function(content) {

        var replacedContent = content.replace(/BLUEPRINT_BASE_NAME/g, this.templatedata.namespace);
        return replacedContent;
    } 

});

module.exports = LambdaDotNetGenerator;