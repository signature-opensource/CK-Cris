export class Result implements IResult, IMoreResult, IAnotherResult, IUnifiedResult {
val: number;

/**
 * Gets or sets the More value.
 **/
moreVal: number;
anotherVal: number;
/**
 * Factory method that exposes all the properties as parameters.
 * @param moreVal The More value.
 **/
static create( 
val: number,
moreVal: number,
anotherVal: number ) : Result

/**
 * Creates a new command and calls a configurator for it.
 * @param config A function that configures the new command.
 **/
static create( config: (c: Result) => void ) : Result
// Implementation.
static create( 
val: number|((c:Result) => void),
moreVal?: number,
anotherVal?: number ) {
const c = new Result();
if( typeof val === 'function' ) val(c);
else {
c.val = val;
c.moreVal = moreVal;
c.anotherVal = anotherVal;
return c;
}

}
}
export interface IResult {
val: number;
}

/**
 * Extends the basic result with a IMoreResult.moreVal.
 **/
export interface IMoreResult extends IResult {

/**
 * Gets or sets the More value.
 **/
moreVal: number;
}
export interface IAnotherResult extends IResult {
anotherVal: number;
}
export interface IUnifiedResult extends IMoreResult, IResult, IAnotherResult {
}
